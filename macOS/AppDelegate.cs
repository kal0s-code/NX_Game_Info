using AppKit;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NX.GameInfo.Core.Services;

namespace NX_Game_Info
{
    [Register("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        private MainWindowController mainWindowController = null!;
        private ServiceProvider? _serviceProvider;

        public AppDelegate()
        {
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            _serviceProvider = BuildServiceProvider();

            var keysetService = _serviceProvider.GetRequiredService<SwitchKeysetService>();
            var processorLogger = _serviceProvider.GetRequiredService<ILogger<GameInfoProcessor>>();
            Process.ConfigureServices(keysetService, processorLogger);

            mainWindowController = new MainWindowController();
            mainWindowController.Window.MakeKeyAndOrderFront(this);
        }

        public override void WillTerminate(NSNotification notification)
        {
            // Insert code here to tear down your application
            _serviceProvider?.Dispose();
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton<SwitchKeysetService>(_ => new SwitchKeysetService(NullLogger<SwitchKeysetService>.Instance));
            services.AddSingleton<ILogger<GameInfoProcessor>>(_ => NullLogger<GameInfoProcessor>.Instance);
            return services.BuildServiceProvider();
        }
    }
}
