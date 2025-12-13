using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Schema;
using System.Threading;
using System.Threading.Tasks;

namespace LocaLink;

public static class Servers
{
	public static int DiscoveryPort = 8888;
    public static int ServerPort = 6000;
	public static DiscoveryServer Server = new DiscoveryServer(DiscoveryPort, ServerPort);
	public static Thread ServerThread = new Thread(() =>
	{
		Server.StartAsync().GetAwaiter().GetResult();
	});
	public static JsonWebSocketServer WsServer = new JsonWebSocketServer(ServerPort);
	public static Thread WsServerThread = new Thread(async () =>
	{
		WsServer.StartAsync();
	});
	public static DiscoveryClient Client = new DiscoveryClient(DiscoveryPort);
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
