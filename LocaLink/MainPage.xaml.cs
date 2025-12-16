using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Maui.Platform;

namespace LocaLink;


public partial class MainPage : ContentPage
{
    private ObservableCollection<WsServerInfo> _serverInfos;
    private ObservableCollection<WsMessage> _wsMessages;
    private ObservableCollection<UserInfo> _userInfos;
    private List<string> _devicesList = [];
    private List<IPDevice> _networkDevices;
    private string _currentDevice = "";
    private int HStartId { get; set; } = -1;
    private int HEndId { get; set; } = -1;
    public ICommand SendCommand { get; }

    public MainPage()
    {
        ResourceDictionary ColorResource = Application.Current.Resources.MergedDictionaries.FirstOrDefault() as ResourceDictionary;
        Color gray200 = ColorResource["Gray200"] as Color;
        InitializeComponent();
        _serverInfos = new ObservableCollection<WsServerInfo>();
        ServersListView.ItemsSource = _serverInfos;
        _wsMessages = new ObservableCollection<WsMessage>();
        MessagesListView.ItemsSource = _wsMessages;
        _userInfos = new ObservableCollection<UserInfo>();
        UsersListView.ItemsSource = _userInfos;
        SendCommand = new Command(OnSendCommand);
        _networkDevices = NetworkDeviceHelper.AvailableIPv4AddressesAndDeviceNames();
        for (int i = 0; i < _networkDevices.Count; i++)
        {
            _devicesList.Add(_networkDevices[i].Device);
        }
        InterfacePicker.ItemsSource = _devicesList;
        InterfacePicker.SelectedIndexChanged += InterfaceSelectedChanged;
        if (_devicesList.Count > 0)
        {
            InterfacePicker.SelectedIndex = 0;
            _currentDevice = _devicesList[0];
        }
    }

    public void OnTestClicked(object sender, EventArgs e)
    {
        Console.WriteLine("test clicked");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.Window.MinimumHeight = 720;
        this.Window.MinimumWidth = 1300;
        this.Window.MaximumHeight = 720;
        this.Window.MaximumWidth = 1300;
        this.Window.Height = 720;
        this.Window.Width = 1300;
    }

    public void OnLocalServerSwitchToggled(object sender, ToggledEventArgs e)
    {
        bool isToggled = e.Value;
        if (UserName.Text != "")
        {
            Servers.Server.Name = UserName.Text;
        } else
        {
            Servers.Server.Name = "Unknown";
        }
        
        if (isToggled)
        {
            Console.WriteLine("Local Server is ON");
            Servers.Server.Enable();
            Servers.WsServer.Enable();
            string IP = "*.*.*.*";
            if (_currentDevice != "")
            {
                IP = NetworkDeviceHelper.GetIPv4Address(_currentDevice);
            }
            LocalServerInfoLabel.Text = $"{Servers.Server.Name}:{IP}:{Servers.Server.Port}";
            Console.WriteLine($"{Servers.Server.Name}, {Servers.Server.Running}, {Servers.WsServer.Running}");
        }
        else
        {
            Console.WriteLine("Local Server is OFF");
            Servers.Server.Disable();
            Servers.WsServer.Disable();
            LocalServerInfoLabel.Text = "";
            Console.WriteLine($"{Servers.Server.Name}, {Servers.Server.Running}, {Servers.WsServer.Running}");
        }
    }

    public void UpdateServers(List<ServerIPPort> servers)
    {
        foreach (var s in servers)
        {
            Console.WriteLine($"{s.Name} => {s.IP}:{s.Port}");
            _serverInfos.Add(new WsServerInfo { Name = s.Name, Info = $"{s.IP}:{s.Port}" });
        }
        RefreshBtn.Text = "Refresh";
        RefreshBtn.IsEnabled = true;
    }

    public void OnRefreshBtnClicked(object sender, EventArgs e)
    {
        Console.WriteLine("Scanning for servers...");
        if (_currentDevice != "")
        {
            _serverInfos.Clear();
            RefreshBtn.Text = "Waiting";
            RefreshBtn.IsEnabled = false;
            var clientThread = new Thread(async () =>
            {
                Servers.Client.DiscoverAsync(this, _currentDevice).GetAwaiter().GetResult();
            });
            clientThread.IsBackground = true;
            clientThread.Start();
        }
        else
        {
            Console.WriteLine("No available network device selected!");
        }
    }

    async public void ServerListSelected(object sender, SelectedItemChangedEventArgs e)
    {
        if (e.SelectedItem != null)
        {
            // Cast the selected item to your model type
            var ServerInfo = (WsServerInfo)e.SelectedItem;
            
            // DisplayAlert("Join Server", $"Name: {ServerInfo.Name}\nAddress: {ServerInfo.Info}", "OK");
            bool answer = await DisplayAlert("Join Server", $"Name: {ServerInfo.Name}\nAddress: {ServerInfo.Info}", "Yes", "No");
            if (answer)
            {
                Console.WriteLine("User chose Yes.");
                var parts = ServerInfo.Info.Split(':');
                if (Servers.WsClient != null)
                {
                    Servers.WsClient.Stop();
                }
                Servers.WsClient = new JsonWebSocketClient($"ws://{parts[0]}:{parts[1]}/ws");
                Servers.WsClientThread = new Thread(async () =>
                {
                    await Servers.WsClient.StartAsync();
                });
                Servers.WsClientThread.IsBackground = true;
                Servers.WsClientThread.Start();
                Servers.WsClient.OnMessage += (msg) =>
                {
                    if (msg.Type == "chat")
                    {
                        Console.WriteLine($"{msg.Type}: {JsonSerializer.Serialize(msg.Data)}");
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            // msg.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            _wsMessages.Add(msg);
                            // MessagesListView.ScrollTo(msg, ScrollToPosition.End, true);
                        });
                    }
                    else if (msg.Type == "join")
                    {
                        Servers.WsClient.SetToken(msg.Token.ToString());
                        Console.WriteLine($"Get Token: {Servers.WsClient.GetToken()}");
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            ServerName.Text = ServerInfo.Name;
                            ServerIPPort.Text = ServerInfo.Info;
                            LeaveBtn.IsEnabled = true;
                            _wsMessages.Clear();
                            for (int i = 0; i < msg.History.Count; i++)
                            {
                                _wsMessages.Add(msg.History[msg.History.Count - i - 1]);
                            }
                            HStartId = msg.StartId - msg.History.Count;
                            HEndId = msg.EndId;
                        });
                    }
                    else if (msg.Type == "load")
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            for (int i = 0; i < msg.History.Count; i++)
                            {
                                _wsMessages.Insert(0, msg.History[i]);
                            }
                            HStartId = msg.StartId - msg.History.Count;
                            HEndId = msg.EndId;
                        });
                    }
                    else if (msg.Type == "notification")
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            msg.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            // Console.WriteLine($"Get {msg.Type}: {msg.Data}");
                            _wsMessages.Add(msg);
                            _userInfos.Clear();
                            for (int i = 0; i < msg.Users.Count; i += 1)
                            {
                                _userInfos.Add(msg.Users[i]);
                            }
                            if (msg.Notification == "<System: server closed>")
                            {
                                if (Servers.WsClient != null)
                                {
                                    Servers.WsClient.Stop();
                                    Servers.WsClient = null;
                                }
                            }
                            // MessagesListView.ScrollTo(msg, ScrollToPosition.End, true);
                        });
                    }
                };

                var name = "Unknown";
                if (UserName.Text != "")
                {
                    name = UserName.Text;
                }

                await Task.Delay(1000);
                var msg = new WsMessage
                {
                    Type = "join",
                    Data = "",
                    Name = name
                };
                Servers.WsClient.SendAsync(msg);
                Console.WriteLine($"Joining server {parts[0]}:{parts[1]} ...");
            }
            else
            {
                Console.WriteLine("User chose No.");
            }

            // Optional: Deselect the item after action (prevents the event from firing repeatedly if re-selected)
            ((ListView)sender).SelectedItem = null;
        }
    }

    public void InterfaceSelectedChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        int selectedIndex = picker.SelectedIndex;
        if (selectedIndex != -1)
        {
            var IP = NetworkDeviceHelper.GetIPv4Address(_devicesList[selectedIndex]);
            _currentDevice = _devicesList[selectedIndex];
            Console.WriteLine($"Seleted Device: {_currentDevice}, IP: {IP}");
        }
    }

    async public void OnLeaveBtnClicked(object sender, EventArgs e)
    {
        bool answer = await DisplayAlert("Leave Server", "Do you want to leave this server?", "Yes", "No");
        if (answer)
        {
            Console.WriteLine("User chose Yes.");
            if (Servers.WsClient != null)
            {
                Servers.WsClient.Stop();
                Servers.WsClient = null;
                Servers.WsClientThread = null;
            }
            _wsMessages.Clear();
            _userInfos.Clear();
            ServerName.Text = "";
            ServerIPPort.Text = "";
            LeaveBtn.IsEnabled = false;
            Console.WriteLine("leave the server.");
        }
        else
        {
            Console.WriteLine("User chose No.");
        }
    }

    async public void OnSendCommand()
    {
        if (Servers.WsClient != null && Servers.WsClient.Opened())
        {
            var name = "Unknown";
            if (UserName.Text != "")
            {
                name = UserName.Text;
            }
            var message = MessageEditor.Text;
            var msg = new WsMessage
            {
                Type = "chat",
                Data = message,
                Token = Servers.WsClient.GetToken(),
                Name = name
            };
            await Servers.WsClient.SendAsync(msg);
            MessageEditor.Text = "";
            Console.WriteLine($"Send {message}");
        } else
        {
            await DisplayAlert("Warning", "Not connected to a server!", "OK");
        }
        Console.WriteLine("Send Message");
    }

    public void OnSendMessageBtnClicked(object sender, EventArgs e)
    {
        OnSendCommand();
    }

    async public void OnMessagesListViewScrolled(object sender, ScrolledEventArgs e)
    {
        // Console.WriteLine($"scroll: {e.ScrollY}");
        if (e.ScrollY <= 0)
        {
            Console.WriteLine($"Reached the top, need to load from history from: {HStartId}");
            if (HStartId > 0)
            {
                var name = "Unknown";
                if (UserName.Text != "")
                {
                    name = UserName.Text;
                }
                var msg = new WsMessage
                {
                    Type = "load",
                    StartId = HStartId,
                    EndId = HEndId,
                    Token = Servers.WsClient.GetToken(),
                    Name = name
                };
                await Servers.WsClient.SendAsync(msg);
            }
        }
    }
}
