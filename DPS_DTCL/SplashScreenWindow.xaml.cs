using DTCL.Messages;
using DTCL.Transport;
using System.IO;
using System.Windows;
using static DTCL.MainWindow;

namespace DTCL
{
    /// <summary>
    /// Interaction logic for SplashScreenWindow.xaml
    /// </summary>
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow(LayoutMode _currentLayout = LayoutMode.DTCLLayout)
        {
            InitializeComponent();

            if (File.Exists(@"DebugLog.txt"))
                File.Delete(@"DebugLog.txt");

            Log.Log.Info("Application started");

            switch (_currentLayout)
            {
                case LayoutMode.DPSLayout:
                    TitleName.Text = "DPS CARTRIDGE LOADER";
                    break;
                case LayoutMode.DTCLLayout:
                    TitleName.Text = "Data Transfer Cartridge Loader";
                    break;
            }

            // FileOperations.createDir(HardwareInfo.Instance.D1UploadFilePath);
            FileOperations.createDir(HardwareInfo.Instance.D2UploadFilePath);
            FileOperations.createDir(HardwareInfo.Instance.D3UploadFilePath);

            // FileOperations.createDir(HardwareInfo.Instance.D1DownloadFilePath);
            FileOperations.createDir(HardwareInfo.Instance.D2DownloadFilePath);
            FileOperations.createDir(HardwareInfo.Instance.D3DownloadFilePath);

        }
    }
}