using DTCL.Log;
using DTCL.Transport;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DTCL.Mux
{
    /// <summary>
    /// Interaction logic for Mux_SelfTest.xaml
    /// </summary>
    public partial class Mux_SelfTest : Window
    {
        UartTransportSync _muxTransport;
        System.Timers.Timer _muxScanTimer = new System.Timers.Timer();
        PopUpMessagesContainer PopUpMessagesContainerObj;
        bool manualModeFlag;

        readonly MuxViewModel viewModel = new MuxViewModel();
        public Mux_SelfTest() => InitializeComponent();

        public Mux_SelfTest(UartTransportSync muxTransport, PopUpMessagesContainer _PopUpMessagesContainerObj)
        {
            InitializeComponent();
            PopUpMessagesContainerObj = _PopUpMessagesContainerObj;
            DataContext = viewModel;
            _muxTransport = muxTransport;
            _muxScanTimer.Interval = 200;
            _muxScanTimer.Elapsed += MuxScanTimer_Elapsed;
            _muxScanTimer.Start();
            Focus();
        }

        void Exit_Click(object sender, RoutedEventArgs e) => Close();

        public int get_MuxPosition()
        {
            var Txbuff = new byte[1];
            var Rxbuff = new byte[4];
            byte temp = 0x30;
            Txbuff[0] = (byte)((byte)(1) + temp);

            if (_muxTransport != null)
            {
                _muxTransport.Send(Txbuff, 0, 1);

                Rxbuff = _muxTransport.WaitForResponse(4, 500);

                if ((Rxbuff[1] == 'M'))
                    return Rxbuff[3];
                else
                {
                    Log.Log.Error($"Failed to get Mux Channel");
                    return -1;
                }
            }
            else
            {
                Log.Log.Error($"Mux Hw Not connected, Failed to get Mux Channel No");
                return -1;
            }
        }

        public void MuxScanTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _muxScanTimer.Stop();

            Application.Current.Dispatcher
                .Invoke(() =>
            {
                viewModel.MuxPosition = get_MuxPosition(); // 49 to 56

                if (viewModel.MuxPosition == -1 && manualModeFlag == false)
                {
                    UpdateUserStatus("USBMux_ManualMode_Msg");
                    manualModeFlag = true;
                }
                else if (viewModel.MuxPosition != -1 && manualModeFlag == true)
                {
                    manualModeFlag = false;
                    UpdateUserStatus("USBMux_Detect_Manual_Msg");
                }
            });

            _muxScanTimer.Start();
        }

        public void UpdateUserStatus(string Msg)
        {
            StatusTextBlock.FontSize = 14;
            StatusTextBlock.Text = PopUpMessagesContainerObj.FindStatusMsgById(Msg);
            // return StatusTextBlock.Text;
        }

        void Window_Closed(object sender, EventArgs e)
        {
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _muxScanTimer?.Stop();
            // _muxScanTimer?.Dispose();
        }
    }

    public class MuxViewModel : INotifyPropertyChanged
    {
        int _muxPosition;
        public int MuxPosition
        {
            get => _muxPosition;
            set
            {
                if (_muxPosition != value)
                {
                    _muxPosition = value;
                    OnPropertyChanged(nameof(MuxPosition));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MuxStrokeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int muxPosition && parameter is string indexStr &&
                int.TryParse(indexStr, out int index))
            {
                var isSelected = (muxPosition - 48 == index);

                if (targetType == typeof(Brush))
                {
                    return isSelected ? Brushes.DodgerBlue : Brushes.ForestGreen;
                }
            }

            return Brushes.ForestGreen;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}