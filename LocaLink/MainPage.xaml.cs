using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace LocaLink;

public class ChatItem
{
	public string Name { get; set; } = "";
	public string Message { get; set; } = "";
	public bool IsSentByUser { get; set; }
}

public partial class MainPage : ContentPage
{
	private ObservableCollection<WsServerInfo> _serverInfos;

	public MainPage()
	{
		ResourceDictionary ColorResource = Application.Current.Resources.MergedDictionaries.FirstOrDefault() as ResourceDictionary;
		Color gray200 = ColorResource["Gray200"] as Color;
		InitializeComponent();
		_serverInfos = new ObservableCollection<WsServerInfo>();
		ServersListView.ItemsSource = _serverInfos;
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
			LocalServerInfoLabel.Text = $"{Servers.Server.Name}:{Servers.Server.Port}";
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
		_serverInfos.Clear();
		RefreshBtn.Text = "Waiting";
		RefreshBtn.IsEnabled = false;
		var clientThread = new Thread(async () =>
		{
			Servers.Client.DiscoverAsync(this).GetAwaiter().GetResult();
		});
		clientThread.IsBackground = true;
		clientThread.Start();
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

	public void OnLeaveBtnClicked(object sender, EventArgs e)
	{
		if (Servers.WsClient != null)
		{
			Servers.WsClient.Stop();
			Console.WriteLine("leave the server.");
			Servers.WsClient = null;
			Servers.WsClientThread = null;
			ServerName.Text = "";
			ServerIPPort.Text = "";
			LeaveBtn.IsEnabled = false;
		}
	}

	public void OnSendMessageBtnClicked(object sender, EventArgs e)
	{

	}
}
