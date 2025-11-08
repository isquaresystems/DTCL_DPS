using DTCL.Log;
using DTCL.Messages;
using DTCL.Transport;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using static DTCL.MainWindow;

namespace DTCL
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShowSplashScreen();
        }

        void Application_Startup(object sender, StartupEventArgs e)
        {

        }

        async void ShowSplashScreen()
        {
            var _currentLayout = LayoutMode.DTCLLayout;
            var _logLevel = LogLevel.Info;

            if (File.Exists(@"Default.txt"))
            {
                var data = FileOperations.ReadFileData(@"Default.txt", 0, FileOperations.getFileSize(@"Default.txt"));
                var configContent = System.Text.Encoding.ASCII.GetString(data);
                var configLines = configContent.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                // Read layout mode from first line
                if (configLines.Length > 0)
                {
                    switch (configLines[0].ToLower())
                    {
                        case "dps":
                            _currentLayout = LayoutMode.DPSLayout;
                            break;
                        case "dtcl":
                            _currentLayout = LayoutMode.DTCLLayout;
                            break;
                    }
                }

                // Read log level from second line if exists
                if (configLines.Length > 1)
                {
                    switch (configLines[1].ToLower())
                    {
                        case "debug":
                            _logLevel = LogLevel.Debug;
                            break;
                        case "info":
                            _logLevel = LogLevel.Info;
                            break;
                        case "warning":
                            _logLevel = LogLevel.Warning;
                            break;
                        case "error":
                            _logLevel = LogLevel.Error;
                            break;
                        case "data":
                            _logLevel = LogLevel.Data;
                            break;
                    }
                }
            }

            Log.Log.SetLogLevel(_logLevel);
            var mainWindow = new MainWindow(_currentLayout);
            var splashScreen = new SplashScreenWindow(_currentLayout);

            splashScreen.Show();
            await Task.Delay(3000);

            mainWindow.Show();

            await Task.Delay(100);

            splashScreen.Close();
        }
    }
}