using downloads_watcher;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "music-download-watcher";
    }).ConfigureServices(services =>
    {
        services.AddHostedService<MusicWatcher>();
    })
    .Build();


await host.RunAsync();
