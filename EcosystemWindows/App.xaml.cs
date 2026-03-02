using System.Windows;

namespace EcosystemWindows
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static void AgentLog(string location, string message, string hypothesisId)
        {
            // #region agent log
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sessionId = "b1bde9",
                    runId = "pre-launch",
                    hypothesisId,
                    location,
                    message,
                    data = new { },
                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
                });
                System.IO.File.AppendAllText("debug-b1bde9.log", json + System.Environment.NewLine);
            }
            catch
            {
                // ignore logging failures
            }
            // #endregion
        }

        public App()
        {
            AgentLog("App.xaml.cs:ctor", "App constructed", "H1");

            // #region agent log
            try
            {
                this.DispatcherUnhandledException += (sender, args) =>
                {
                    AgentLog("App.xaml.cs:DispatcherUnhandledException", args.Exception.ToString(), "HEX");
                };

                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    AgentLog("App.xaml.cs:UnhandledException", args.ExceptionObject?.ToString() ?? "null", "HEX");
                };

                System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, args) =>
                {
                    AgentLog("App.xaml.cs:UnobservedTaskException", args.Exception.ToString(), "HEX");
                };
            }
            catch
            {
            }
            // #endregion
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            AgentLog("App.xaml.cs:OnStartup:begin", "OnStartup entered", "H1");

            base.OnStartup(e);
            
            // Initialize notification manager
            Microsoft.Toolkit.Uwp.Notifications.ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                AgentLog("App.xaml.cs:OnStartup:ToastActivated", "Toast activation handler subscribed", "H2");

                // Handle toast activation
                Dispatcher.BeginInvoke(() =>
                {
                    var mainWindow = Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.Activate();
                    }
                });
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Microsoft.Toolkit.Uwp.Notifications.ToastNotificationManagerCompat.Uninstall();
            base.OnExit(e);
        }
    }
}