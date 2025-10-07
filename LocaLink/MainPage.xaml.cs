namespace LocaLink;

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
		this.Window.MinimumHeight = 400;
		this.Window.MinimumWidth = 450;
		this.Window.Height = 400;
		this.Window.Width = 450;
	}

	public void OnSendMessageBtnClicked(object sender, EventArgs e)
	{

	}
}
