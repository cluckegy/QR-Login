using System;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceSystem
{
    public partial class SessionWindow : Window
    {
        public SessionInfo? SelectedSession { get; private set; }

        public SessionWindow()
        {
            InitializeComponent();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbDepartment.SelectedItem == null || cmbGrade.SelectedItem == null)
                {
                    CustomMessageBox.Show("يرجى التأكد من البيانات", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Parse login time (support both comma and dot as decimal separator)
                string loginInput = txtLoginTime.Text.Trim().Replace(",", ".");
                string lectureInput = txtLectureTime.Text.Trim().Replace(",", ".");

                if (!double.TryParse(loginInput, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out double loginTime))
                {
                    CustomMessageBox.Show("يرجى إدخال وقت تسجيل الدخول بشكل صحيح (مثال: 5 أو 1.5)", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!double.TryParse(lectureInput, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out double lectureTime))
                {
                    CustomMessageBox.Show("يرجى إدخال وقت المحاضرة بشكل صحيح (مثال: 60 أو 45.5)", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (loginTime <= 0)
                {
                    CustomMessageBox.Show("وقت تسجيل الدخول يجب أن يكون أكبر من صفر", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (lectureTime <= 0)
                {
                    CustomMessageBox.Show("وقت المحاضرة يجب أن يكون أكبر من صفر", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (loginTime > lectureTime)
                {
                    CustomMessageBox.Show("وقت تسجيل الدخول لا يمكن أن يكون أكبر من وقت المحاضرة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string dept = (cmbDepartment.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "معلم حاسب آلي إنجليزي";
                string gradeStr = (cmbGrade.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                
                int grade = 1;
                if (gradeStr.Contains("الثانية")) grade = 2;
                else if (gradeStr.Contains("الثالثة")) grade = 3;
                else if (gradeStr.Contains("الرابعة")) grade = 4;

                SelectedSession = new SessionInfo
                {
                    SessionId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    Department = dept,
                    Grade = grade,
                    LoginDurationMinutes = loginTime,
                    LectureDurationMinutes = lectureTime,
                    StartTime = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"حدث خطأ غير متوقع: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void cmbGrade_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
