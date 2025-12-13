namespace LocaLink;

public class ChatItem
{
	public string Name { get; set; } = "";
	public string Message { get; set; } = "";
	public bool IsSentByUser { get; set; }
}

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		ResourceDictionary ColorResource = Application.Current.Resources.MergedDictionaries.FirstOrDefault() as ResourceDictionary;
		Color gray200 = ColorResource["Gray200"] as Color;
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		this.Window.MinimumHeight = 600;
		this.Window.MinimumWidth = 1040;
		this.Window.Height = 600;
		this.Window.Width = 1040;
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

	public void OnSendMessageBtnClicked(object sender, EventArgs e)
	{

	}
}
