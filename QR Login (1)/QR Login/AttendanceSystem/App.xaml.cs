using System;
using System.Windows;
using System.Windows.Threading;

namespace AttendanceSystem
{
    public partial class App : Application
    {
        public App()
        {
            // Handle UI thread exceptions
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            // Handle non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            
            // Handle task exceptions
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = GetErrorMessage(e.Exception);
            
            try
            {
                CustomMessageBox.Show($"حدث خطأ غير متوقع:\n\n{errorMessage}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                MessageBox.Show($"حدث خطأ غير متوقع:\n\n{errorMessage}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            e.Handled = true; // Prevent crash
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                string errorMessage = GetErrorMessage(ex);
                
                try
                {
                    MessageBox.Show($"حدث خطأ حرج:\n\n{errorMessage}", "خطأ حرج", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            string errorMessage = GetErrorMessage(e.Exception);
            
            try
            {
                Dispatcher.Invoke(() =>
                {
                    CustomMessageBox.Show($"خطأ في عملية خلفية:\n\n{errorMessage}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            catch { }
            
            e.SetObserved(); // Mark as handled
        }

        private string GetErrorMessage(Exception ex)
        {
            if (ex == null) return "خطأ غير معروف";
            
            string message = ex.Message;
            
            // Get inner exception message if available
            if (ex.InnerException != null)
            {
                message += $"\n\nالسبب: {ex.InnerException.Message}";
            }
            
            // Limit message length
            if (message.Length > 500)
            {
                message = message.Substring(0, 500) + "...";
            }
            
            return message;
        }
    }
}
