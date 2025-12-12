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
		this.Window.MinimumWidth = 1024;
		this.Window.Height = 600;
		this.Window.Width = 1024;
	}

	public void OnSendMessageBtnClicked(object sender, EventArgs e)
	{

	}
}
