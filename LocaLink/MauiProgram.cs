using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Schema;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.LifecycleEvents;

namespace LocaLink;

public static class Servers
{
	public static int DiscoveryPort = 8888;
    public static int ServerPort = 9999; // disable firewall, admin cmd run: netsh http add urlacl url=http://+:9999/ user=Everyone
	public static DiscoveryServer Server = new DiscoveryServer(DiscoveryPort, ServerPort);
	public static Thread ServerThread = new Thread(() =>
	{
		Server.StartAsync().GetAwaiter().GetResult();
	});
	public static JsonWebSocketServer WsServer = new JsonWebSocketServer(ServerPort);
	public static Thread WsServerThread = new Thread(async () =>
	{
		await WsServer.StartAsync();
	});
	public static DiscoveryClient Client = new DiscoveryClient(DiscoveryPort);
	public static JsonWebSocketClient WsClient = new JsonWebSocketClient("");
	public static Thread WsClientThread;
}

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});
		builder.Services.AddSingleton<MainPage>();

#if WINDOWS
        builder.ConfigureLifecycleEvents(events =>
        {
            // Make sure to add "using Microsoft.Maui.LifecycleEvents;" in the top of the file 
            events.AddWindows(windowsLifecycleBuilder =>
            {
                windowsLifecycleBuilder.OnWindowCreated(window =>
                {
                    window.ExtendsContentIntoTitleBar = false;
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
                    switch (appWindow.Presenter)
                    {
                        case Microsoft.UI.Windowing.OverlappedPresenter overlappedPresenter:
							overlappedPresenter.IsMaximizable = false;
                            // overlappedPresenter.SetBorderAndTitleBar(false, false);
                            // overlappedPresenter.Maximize();
                            break;
                    }
                });
            });
        });
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif
		// int discoveryPort = 8888;
        // int serverPort = 6000;
        // var server = new DiscoveryServer(discoveryPort, serverPort);
        // var serverThread = new Thread(() =>
        // {
        //     server.StartAsync().GetAwaiter().GetResult();
        // });

        Servers.ServerThread.IsBackground = true;
        Servers.ServerThread.Start();
        // var client = new DiscoveryClient(discoveryPort);
        // var wsServer = new JsonWebSocketServer(serverPort);
        // var wsServerThread = new Thread(async () =>
        // {
        //     wsServer.StartAsync();
        // });
        Servers.WsServerThread.IsBackground = true;
        Servers.WsServerThread.Start();

		return builder.Build();
	}
}
