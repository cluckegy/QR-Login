using System;
using System.Windows;

namespace AttendanceSystem
{
    public partial class CustomMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        public CustomMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage image)
        {
            InitializeComponent();
            txtTitle.Text = title;
            txtMessage.Text = message;

            if (button == MessageBoxButton.YesNo)
            {
                btnYes.Visibility = Visibility.Visible;
                btnNo.Visibility = Visibility.Visible;
                btnOk.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnOk.Visibility = Visibility.Visible;
                btnYes.Visibility = Visibility.Collapsed;
                btnNo.Visibility = Visibility.Collapsed;
            }

            // Set icon color based on image type (Optional enhancement)
             if (image == MessageBoxImage.Error) txtTitle.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["DangerBrush"];
             else if (image == MessageBoxImage.Warning) txtTitle.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];
             else if (image == MessageBoxImage.Information) txtTitle.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];
        }

        public static MessageBoxResult Show(string message, string title = "Message", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        {
            var msgBox = new CustomMessageBox(message, title, button, image);
            msgBox.ShowDialog();
            return msgBox.Result;
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close(); // Default result is Cancel/None
        }
    }
}
