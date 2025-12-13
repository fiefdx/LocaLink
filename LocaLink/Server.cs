using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace LocaLink;

public class DiscoveryServer
{
    private readonly UdpClient udp;
    public string Name { get; set; } = "Unknown";
    public int Port { get; private set; } = 6000;
    public bool Running = false;

    public DiscoveryServer(int listenPort, int port = 6000)
    {
        udp = new UdpClient(listenPort);
        Port = port;
    }

    public void Enable()
    {
        Running = true;
    }

    public void Disable()
    {
        Running = false;
    }

    public async Task StartAsync()
    {
        // Console.WriteLine($"Discovery server listening on {udp.Client.LocalEndPoint}");
        while (true)
        {
            if (Running)
            {
                // var receiveTask = udp.ReceiveAsync();
                // var timeoutTask = Task.Delay(2000);
                // var completed = await Task.WhenAny(receiveTask, timeoutTask);
                // string msg = "";
                // var result = default(UdpReceiveResult);
                // if (completed == receiveTask)
                // {
                //     result = receiveTask.Result;
                //     msg = Encoding.UTF8.GetString(result.Buffer);
                // } else
                // {
                //     // timeout
                //     // Console.WriteLine("Timeout waiting for discovery requests.");
                //     continue;
                // }

                var result = await udp.ReceiveAsync();
                string msg = Encoding.UTF8.GetString(result.Buffer);

                // Console.WriteLine($"Ping from {result.RemoteEndPoint}: {msg}");

                if (msg == "DISCOVER_REQUEST" && Running)
                {
                    string reply = $"DISCOVER_RESPONSE_LOCALINK;PORT={Port};NAME={Name}";
                    byte[] data = Encoding.UTF8.GetBytes(reply);

                    // Respond directly to the sender (unicast)
                    await udp.SendAsync(data, data.Length, result.RemoteEndPoint);
                    // Console.WriteLine("Sent discovery response.");
                }
            }
            else
            {
                await Task.Delay(100);
                // continue;
            }
        }
    }
}

public class WsServerInfo
{
    public string Name { get; set; } = "";
    public string Info { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 0;
}


public class WsMessage
{
    public string Type { get; set; } = "";
    public object? Data { get; set; }
    public string? Token { get; set; }
    public string? Name { get; set; }
    public string? Message { get; set; }
    public string? Time { get; set; }
    public string? Notification { get; set; } = "";
}


public class User
{
    public string Name { get; set; } = "Guest";
    private string Token { get; set; } = "";
    public string IP { get; set; } = "";
    public int Port { get; set; } = 0;
    public WebSocket Socket { get; set; }

    public User(string name, string token, WebSocket socket)
    {
        Name = name;
        Token = token;
        Socket = socket;
    }

    public string GetToken()
    {
        return Token;
    }
}


public class JsonWebSocketServer
{
    public int Port { get; set; } = 6000;
    public bool Running = false;
    public int TokenCounter = 0;
    private List<User> users = new List<User>();
    private string ManagerToken = "";

    public JsonWebSocketServer(int port)
    {
        Port = port;
    }

    public string GenerateToken()
    {
        TokenCounter++;
        return $"token_{TokenCounter}_{DateTime.UtcNow.Ticks}";
    }

    public void Enable()
    {
        Running = true;
    }

    public void Disable()
    {
        Running = false;
    }

    public bool IsLocalConnection(string ip)
    {
        var ips = Dns.GetHostAddresses(Dns.GetHostName());
        foreach (var localIp in ips)
        {
            if (localIp.ToString() == ip)
            {
                return true;
            }
        }
        return false;
    }

    public async Task StartAsync()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add($"http://+:{Port}/");
        listener.Start();

        // Console.WriteLine($"JSON WebSocket server started at ws://localhost:{Port}/ws");

        while (true)
        {
            if (Running)
            {
                // Console.WriteLine("websocket server running ...");
                var ctx = await listener.GetContextAsync();

                if (!ctx.Request.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.Close();
                    continue;
                }

                _ = HandleClientAsync(ctx);
            }
            else
            {
                // Console.WriteLine("websocket server not running ...");
                await Task.Delay(100);
                continue;
            }
            await Task.Delay(500);
        }
    }

    private async Task HandleClientAsync(HttpListenerContext ctx)
    {
        var clientIp = ctx.Request.RemoteEndPoint.Address;
        var wsCtx = await ctx.AcceptWebSocketAsync(null);
        var socket = wsCtx.WebSocket;

        // Console.WriteLine("Client connected.");

        var buffer = new byte[8192];

        try
        {
            while (socket.State == WebSocketState.Open && Running)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var msg = JsonSerializer.Deserialize<WsMessage>(json)!;

                Console.WriteLine($"[Server] Received {msg.Type}: {json}");

                if (msg.Type == "join")
                {
                    // Console.Write($"\bUser joining: {msg.Name}, from {clientIp}\n>");
                    var isLocal = IsLocalConnection(clientIp.ToString());
                    // Console.Write($"\bUser joining: {msg.Name}, from {clientIp} (Local Manager: {isLocal})\n>");
                    var token = GenerateToken();
                    if (isLocal)
                    {
                        ManagerToken = token;
                        // Console.WriteLine("Assigned as Manager.");
                    }
                    users.Add(new User(msg.Name?.ToString() ?? "Unknown", token, socket));
                    // Console.WriteLine($"User joined: {msg.Name}, assigned token: {token}");
                    await SendJsonAsync(socket, new WsMessage
                    {
                        Type = "join",
                        Data = DateTime.UtcNow.ToString(),
                        Token = token
                    });
                    await SendJsonAsyncBroadcast(new WsMessage
                    {
                        Type = "notification",
                        Name = "System",
                        Notification = $"<System: {msg.Name} join this server!>",
                    });
                }

                // Example: broadcast
                if (msg.Type == "chat")
                {
                    await SendJsonAsyncBroadcast(msg);
                }
            }
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"[Server] WebSocket error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] Client error: {ex}");
        }
        finally
        {
            await HandleClientDisconnectedAsync(socket);
        }

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
        // Console.WriteLine("Client disconnected.");
    }

    private async Task HandleClientDisconnectedAsync(WebSocket socket)
    {
        User? user = null;
        for (int i = 0; i < users.Count; i += 1)
        {
            if (users[i].Socket == socket)
            {
                user = users[i];
                break;
            }
        }
        if (user != null)
        {
            users.Remove(user);

            Console.WriteLine($"[Server] User disconnected: {user.Name}");

            await SendJsonAsyncBroadcast(new WsMessage
            {
                Type = "notification",
                Name = "System",
                Notification = $"<System: {user.Name} leave this server!>",
            });
        }

        if (socket.State != WebSocketState.Closed)
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Connection closed",
                CancellationToken.None);
        }
    }

    private async Task SendJsonAsync(WebSocket socket, WsMessage msg)
    {
        string json = JsonSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        for (int i = 0; i < users.Count; i += 1)
        {
            if (msg.Type == "chat" && users[i].Socket == socket && socket.State == WebSocketState.Open)
            {
                await users[i].Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                break;
            }
            else if (msg.Type == "join" && users[i].Socket == socket && socket.State == WebSocketState.Open)
            {
                await users[i].Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                break;
            }
        }
        // await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task SendJsonAsyncBroadcast(WsMessage msg)
    {
        string json = JsonSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        for (int i = 0; i < users.Count; i += 1)
        {
            if (msg.Type == "chat") // && socket.State == WebSocketState.Open) // users[i].Socket != socket &&
            {
                await users[i].Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else if (msg.Type == "join") // && socket.State == WebSocketState.Open)
            {
                await users[i].Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                await users[i].Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        // await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}

