using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace LocaLink;

public class DiscoveryServer // udp server, for client to discovery websocket server in the same local network
{
    private UdpClient udp;
    public string Name { get; set; } = "Unknown";
    public int Port { get; private set; } = 6000;
    public bool Running = false;

    public DiscoveryServer(int listenPort, int port = 6000)
    {
        udp = new UdpClient(listenPort);
        Port = port;
    }

    public void Enable() // enable this server
    {
        Running = true;
    }

    public void Disable() // disable this server
    {
        Running = false;
    }

    public async Task StartAsync()
    {
        while (true)
        {
            if (Running)
            {
                try
                {
                    var result = await udp.ReceiveAsync();
                    string msg = Encoding.UTF8.GetString(result.Buffer);
                    if (msg == "DISCOVER_REQUEST" && Running)
                    {
                        string reply = $"DISCOVER_RESPONSE_LOCALINK;PORT={Port};NAME={Name}";
                        byte[] data = Encoding.UTF8.GetBytes(reply);

                        // Respond directly to the sender (unicast)
                        await udp.SendAsync(data, data.Length, result.RemoteEndPoint);
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Socket error: {ex.Message}");
                }
                catch (ObjectDisposedException)
                {
                    udp = new UdpClient(Port);
                }
            }
            else
            {
                await Task.Delay(100);
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
    public List<UserInfo> Users { get; set; } = [];
    public int StartId { get; set; } = 0;
    public int EndId { get; set; } = 0;
    public List<WsMessage> History { get; set; } = [];
}

public class UserInfo
{
    public string Name { get; set; } = "";
    public string Time { get; set; } = "";
}

public class User
{
    public string Name { get; set; } = "Unknown";
    private string Token { get; set; } = "";
    public string IP { get; set; } = "";
    public int Port { get; set; } = 0;
    public string Time { get; set; } = "";
    public WebSocket Socket { get; set; }

    public User(string name, string token, WebSocket socket, string time)
    {
        Name = name;
        Token = token;
        Socket = socket;
        Time = time;
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
    private string ManagerToken = ""; // for future use

    public JsonWebSocketServer(int port)
    {
        Port = port;
    }

    public List<UserInfo> GetUsers()
    {
        List<UserInfo> result = [];
        for (int i = 0; i < users.Count; i += 1)
        {
            result.Add(new UserInfo {Name = users[i].Name, Time = users[i].Time});
        }
        return result;
    }

    public User GetUser(string token)
    {
        User? result = null;
        for (int i = 0; i < users.Count; i += 1)
        {
            if (users[i].GetToken() == token)
            {
                return users[i];
            }
        }
        return result;
    }

    public string GenerateToken()
    {
        TokenCounter++;
        return $"token_{TokenCounter}_{DateTime.UtcNow.Ticks}";
    }

    public void Enable() // enable this server
    {
        Running = true;
    }

    async public void Disable() // disable this server
    {
        Running = false;
        var response = new WsMessage
        {
            Type = "notification",
            Name = "System",
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Notification = $"<System: server closed>"
        };
        Storage.Add(response);
        await SendJsonAsyncBroadcast(response);
        for (int i = 0; i < users.Count; i++)
        {
            await users[i].Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
        }
        users.Clear();
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

        while (true)
        {
            if (Running)
            {
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
                await Task.Delay(100);
                continue;
            }
        }
    }

    private async Task HandleClientAsync(HttpListenerContext ctx)
    {
        var clientIp = ctx.Request.RemoteEndPoint.Address;
        var wsCtx = await ctx.AcceptWebSocketAsync(null);
        var socket = wsCtx.WebSocket;
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

                if (Running)
                {
                    if (msg.Type == "join") // processing join request
                    {
                        var isLocal = IsLocalConnection(clientIp.ToString());
                        var token = GenerateToken();
                        if (isLocal)
                        {
                            ManagerToken = token;
                        }
                        users.Add(new User(msg.Name?.ToString() ?? "Unknown", token, socket, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                        int maxId = Storage.MaxID();
                        List<WsMessage> history = Storage.GetRecentsFromID(maxId);
                        await SendJsonAsync(socket, new WsMessage
                        {
                            Type = "join",
                            Data = DateTime.UtcNow.ToString(),
                            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Token = token,
                            StartId = maxId,
                            EndId = maxId,
                            History = history
                        });
                        var response = new WsMessage
                        {
                            Type = "notification",
                            Name = "System",
                            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Notification = $"<System: {msg.Name} join this server>",
                            Users = GetUsers()
                        };
                        Storage.Add(response);
                        await SendJsonAsyncBroadcast(response);
                    }
                    else if (msg.Type == "chat") // processing chat request
                    {
                        if (GetUser(msg.Token) != null)
                        {
                            msg.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            Storage.Add(msg);
                            await SendJsonAsyncBroadcast(msg);
                        }
                    }
                    else if (msg.Type == "load") // processing history load request
                    {
                        List<WsMessage> history = Storage.GetRecentsFromID(msg.StartId);
                        await SendJsonAsync(socket, new WsMessage
                        {
                            Type = "load",
                            Data = DateTime.UtcNow.ToString(),
                            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Token = msg.Token,
                            StartId = msg.StartId,
                            EndId = msg.EndId,
                            History = history
                        });
                    }
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

            var response = new WsMessage
            {
                Type = "notification",
                Name = "System",
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Notification = $"<System: {user.Name} leave this server>",
                Users = GetUsers()
            };
            Storage.Add(response);
            await SendJsonAsyncBroadcast(response);
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
            if (users[i].Socket == socket && socket.State == WebSocketState.Open)
            {
                await users[i].Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                break;
            }
        }
    }

    private async Task SendJsonAsyncBroadcast(WsMessage msg)
    {
        string json = JsonSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        for (int i = 0; i < users.Count; i += 1)
        {
            try
            {
                if (msg.Type == "chat")
                {
                    await users[i].Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else if (msg.Type == "join")
                {
                    await users[i].Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    await users[i].Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch(System.Net.WebSockets.WebSocketException ex) {
                Console.WriteLine(ex);
                if (users[i].Socket.State != WebSocketState.Closed)
                {
                    await users[i].Socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed",
                        CancellationToken.None);
                }
                users.Remove(users[i]);
            }
        }
    }
}
