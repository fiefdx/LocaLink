using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.WebSockets;
using System.Text.Json;
using System.Runtime.Serialization;
using System.Net.NetworkInformation;

namespace LocaLink;


public class IPDevice
{
    public string Device { get; set; } = "";
    public string IP { get; set; } = "";
}

public class NetworkDeviceHelper
{
    public static void DisplayNetworkInterfaces()
    {
        // Get all network interfaces on the local computer
        NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
        Console.WriteLine("Network Interfaces found: " + adapters.Length);

        foreach (NetworkInterface adapter in adapters)
        {
            Console.WriteLine(new string('-', 40));
            Console.WriteLine("Description: {0}", adapter.Description);
            Console.WriteLine("Name: {0}", adapter.Name);
            Console.WriteLine("Id: {0}", adapter.Id);
            Console.WriteLine("Type: {0}", adapter.NetworkInterfaceType);
            Console.WriteLine("Status: {0}", adapter.OperationalStatus);
            Console.WriteLine("MAC Address: {0}", string.Join(":", adapter.GetPhysicalAddress().GetAddressBytes()));
        }
    }

    public static List<IPDevice> AvailableIPv4AddressesAndDeviceNames()
    {
        List<IPDevice> result = [];
        // Console.WriteLine("Available IPv4 Addresses and Device Names:");

        // Get all network interfaces on the local computer
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (NetworkInterface networkInterface in networkInterfaces)
        {
            // Filter out non-active or irrelevant interfaces (optional, but helpful)
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            // Get the IP properties for the current interface
            IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();

            // Iterate through the Unicast IP addresses associated with this interface
            foreach (UnicastIPAddressInformation addressInfo in ipProperties.UnicastAddresses)
            {
                // We are only interested in IPv4 addresses
                if (addressInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    // Ignore loopback addresses (127.0.0.1)
                    if (IPAddress.IsLoopback(addressInfo.Address))
                    {
                        continue;
                    }

                    // Print the IPv4 address and the network adapter's description (device name)
                    result.Add(new IPDevice{IP = addressInfo.Address.ToString(), Device = networkInterface.Description});
                    // Console.WriteLine($"Address: {addressInfo.Address} | Device Name: {networkInterface.Description}");
                }
            }
        }
        return result;
    }

    public static string GetIPv4Address(string deviceName)
    {
        string result = "";

        // Get all network interfaces on the local computer
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (NetworkInterface networkInterface in networkInterfaces)
        {
            // Filter out non-active or irrelevant interfaces (optional, but helpful)
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            // Get the IP properties for the current interface
            IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();

            // Iterate through the Unicast IP addresses associated with this interface
            foreach (UnicastIPAddressInformation addressInfo in ipProperties.UnicastAddresses)
            {
                // We are only interested in IPv4 addresses
                if (addressInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    // Ignore loopback addresses (127.0.0.1)
                    if (IPAddress.IsLoopback(addressInfo.Address))
                    {
                        continue;
                    }

                    if (networkInterface.Description == deviceName)
                    {
                        result = addressInfo.Address.ToString();
                        break;
                    }
                }
            }
        }
        return result;
    }
}

public class ServerIPPort
{
    public string IP { get; set; }
    public int Port { get; set; }
    public string Name { get; set; }

    public ServerIPPort(string ip, int port, string name)
    {
        IP = ip;
        Port = port;
        Name = name;
    }

    public override string ToString()
    {
        return $"{Name} ({IP}:{Port})";
    }

    public void ConsoleOutput()
    {
        Console.WriteLine($"{Name} at {IP}:{Port}");
    }
}

public class DiscoveryClient
{
    // private UdpClient udp;
    // private IPEndPoint broadcastEP;
    private List<ServerIPPort> servers = new List<ServerIPPort>();
    private readonly int listenPort;

    public DiscoveryClient(int port)
    {
        listenPort = port;
    }

    public List<ServerIPPort> GetDiscoveredServers()
    {
        return servers;
    }

    public async Task DiscoverAsync(MainPage page, string deviceName)
    {
        IPAddress localIpAddress = IPAddress.Parse(NetworkDeviceHelper.GetIPv4Address(deviceName));
        IPEndPoint localEndPoint = new IPEndPoint(localIpAddress, 8899);
        var udp = new UdpClient(localEndPoint);
        udp.EnableBroadcast = true;
        udp.Client.ReceiveTimeout = 3000; // optional timeout

        // IPAddress broadcastAddress = IPAddress.Parse("255.255.255.255");
        var broadcastEP = new IPEndPoint(IPAddress.Broadcast, listenPort);
        Console.WriteLine($"Broadcasting discovery request to {broadcastEP}, {IPAddress.Broadcast}");
        byte[] msg = Encoding.UTF8.GetBytes("DISCOVER_REQUEST");

        // Send broadcast
        await udp.SendAsync(msg, msg.Length, broadcastEP);
        // Console.WriteLine("Broadcasted discovery request.");

        // Listen for responses (possibly multiple)
        DateTime end = DateTime.Now.AddSeconds(3);
        // Console.WriteLine($"{DateTime.Now.ToString()} => {end.ToString()}");

        servers.Clear();
        while (DateTime.Now < end)
        {
            // Console.WriteLine(DateTime.Now.ToString() + " Waiting for responses...");
            try
            {
                var receiveTask = udp.ReceiveAsync();
                var timeoutTask = Task.Delay(3000);
                var completed = await Task.WhenAny(receiveTask, timeoutTask);
                string reply = "";
                var result = default(UdpReceiveResult);
                if (completed == receiveTask)
                {
                    result = receiveTask.Result;
                    reply = Encoding.UTF8.GetString(result.Buffer);
                } else
                {
                    // timeout
                    // Console.WriteLine("Receive timeout.");
                    continue;
                }
                
                // Console.WriteLine($"Found service: {result.RemoteEndPoint} => {reply}");
                if (reply.Split(';')[0] == "DISCOVER_RESPONSE_LOCALINK")
                {
                    var parts = reply.Split(';');
                    int port = 0;
                    string name = "Unknown";
                    foreach (var part in parts)
                    {
                        if (part.StartsWith("PORT="))
                        {
                            int.TryParse(part.Substring(5), out port);
                        }
                        if (part.StartsWith("NAME="))
                        {
                            name = part.Substring(5);
                        }
                    }
                    var server = new ServerIPPort(result.RemoteEndPoint.Address.ToString(), port, name);
                    servers.Add(server);
                    // server.ConsoleOutput();
                }
            }
            catch (SocketException)
            {
                // timeout
                // Console.WriteLine("SocketException");
                break;
            }
        }
        udp.Close();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            page.UpdateServers(servers);
        });
        
        // Console.WriteLine("Discovery finished.");
    }
}


public class JsonWebSocketClient
{
    private ClientWebSocket socket = new();
    private Uri uri;
    private bool running = true;
    private string Token { get; set; } = "";

    public event Action<WsMessage>? OnMessage;

    public void SetToken(string token)
    {
        Token = token;
    }

    public string GetToken()
    {
        return Token;
    }

    public JsonWebSocketClient(string url)
    {
        if (url != "")
        {
            uri = new Uri(url);
        }
    }

    public void OpenUri(string url)
    {
        Stop();
        uri = new Uri(url);
        running = true;
    }

    public async Task StartAsync()
    {
        while (running)
        {
            try
            {
                socket = new ClientWebSocket();
                // Console.WriteLine("[Client] Connecting...");
                await socket.ConnectAsync(uri, CancellationToken.None);
                // Console.WriteLine("[Client] Connected!");

                await ReceiveLoop();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Client] Error: " + ex.Message + ", " + uri.AbsoluteUri);
            }

            // Console.WriteLine("[Client] Reconnecting in 3 seconds...");
            await Task.Delay(3000);
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[8192]; // 8k

        while (socket.State == WebSocketState.Open)
        {
            try
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var msg = JsonSerializer.Deserialize<WsMessage>(json)!;

                OnMessage?.Invoke(msg);
            }
            catch
            {
                break;
            }
        }
    }

    public async Task SendAsync(WsMessage msg)
    {
        if (socket.State != WebSocketState.Open)
            return;

        string json = JsonSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public void Stop()
    {
        running = false;
        if (socket.State == WebSocketState.Open)
            socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
    }
}
