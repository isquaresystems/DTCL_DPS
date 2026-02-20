using DTCL.Log;
using System;
using System.Windows;
using System.Windows.Input;

namespace DTCL
{
    public partial class CustomMessageBox : Window
    {
        public enum MessageBoxResult { Yes, No, Ok, Cancel }
        public enum MessageBoxIcon { None, Error, Warning, Information }

        public MessageBoxResult Result { get; private set; }

        public CustomMessageBox(PopUpMessages message, string AdditionalInfo = "")
        {
            InitializeComponent();

            // Set the message text and font size
            MessageTextBlock.Text = message.MessageText + AdditionalInfo;
            MessageTextBlock.FontSize = message.FontSize;
            MessageTextBlock.FontWeight = FontWeights.Bold;

            // Display buttons based on the MessageBoxButton parameter
            switch (message.MessageBoxButtons)
            {
                case "OK":
                    OkButton.Visibility = Visibility.Visible;
                    break;
                case "OKCancel":
                    OkButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    break;
                case "YesNo":
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    break;
                case "YesNoCancel":
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    break;
            }

            // Display the appropriate icon
            switch (message.MessageBoxIcon)
            {
                case "Error":
                    // IconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/error.png")); // Add your error icon path here
                    break;
                case "Warning":
                    // IconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/warning.png")); // Add your warning icon path here
                    break;
                case "Information":
                    // IconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/info.png")); // Add your information icon path here
                    break;
                default:
                    // IconImage.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        public CustomMessageBox(string AdditionalInfo, PopUpMessages message)
        {
            InitializeComponent();

            // Set the message text and font size
            MessageTextBlock.Text = AdditionalInfo + " " + message.MessageText;
            MessageTextBlock.FontSize = message.FontSize;
            MessageTextBlock.FontWeight = FontWeights.Bold;

            // Display buttons based on the MessageBoxButton parameter
            switch (message.MessageBoxButtons)
            {
                case "OK":
                    OkButton.Visibility = Visibility.Visible;
                    break;
                case "OKCancel":
                    OkButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    break;
                case "YesNo":
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    break;
                case "YesNoCancel":
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    break;
            }

            // Display the appropriate icon
            switch (message.MessageBoxIcon)
            {
                case "Error":
                    // IconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/error.png")); // Add your error icon path here
                    break;
                case "Warning":
                    // IconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/warning.png")); // Add your warning icon path here
                    break;
                case "Information":
                    // IconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/info.png")); // Add your information icon path here
                    break;
                default:
                    // IconImage.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        // Button click event handlers
        void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Ok;
            Close();
        }

        void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }

        public static MessageBoxResult Show(PopUpMessages message, Window parent, string AdditionalInfo = "")
        {
            if (message == null)
                return MessageBoxResult.Yes;

            try
            {
                var box = new CustomMessageBox(message, AdditionalInfo);

                // Only set Owner if parent window is valid and already shown
                // This prevents "Cannot set Owner property to a Window that has not been shown previously" error
                if (parent != null && parent.IsLoaded && parent.IsVisible)
                {
                    box.Owner = parent;
                    box.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    // Parent not available - center on screen instead
                    box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                box.ShowDialog();

                return box.Result;
            }
            catch (Exception ex)
            {
                Log.Log.Error($"MessageBoxResult exception : {ex.Message}");
                return MessageBoxResult.Ok;
            }
        }

        public static MessageBoxResult Show2(PopUpMessages message, Window parent, string AdditionalInfo = "")
        {
            if (message == null)
                return MessageBoxResult.Yes;

            try
            {
                var box = new CustomMessageBox(AdditionalInfo, message);

                // Only set Owner if parent window is valid and already shown
                // This prevents "Cannot set Owner property to a Window that has not been shown previously" error
                if (parent != null && parent.IsLoaded && parent.IsVisible)
                {
                    box.Owner = parent;
                    box.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    // Parent not available - center on screen instead
                    box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                box.ShowDialog();

                return box.Result;
            }
            catch (Exception ex)
            {
                Log.Log.Error($"MessageBoxResult exception : {ex.Message}");
                return MessageBoxResult.Ok;
            }
        }

        void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}