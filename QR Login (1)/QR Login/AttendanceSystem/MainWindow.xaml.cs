using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using QRCoder;

namespace AttendanceSystem
{
    public partial class MainWindow : Window
    {
        // Server variables
        private HttpListener? _httpListener;
        private Thread? _serverThread;
        private TcpListener? _httpsListener;
        private Thread? _httpsThread;
        private X509Certificate2? _serverCertificate;
        private volatile bool _isRunning = false;
        private string _serverUrl = "";
        
        // Session data
        private SessionInfo? _currentSession;
        private List<Student> _students = new List<Student>();
        private Dictionary<string, DateTime> _visitorActivity = new Dictionary<string, DateTime>();
        private volatile int _activeVisitors = 0;
        private readonly object _visitorsLock = new();
        private readonly object _studentsLock = new();

        // Timers
        private DispatcherTimer _timerSession;
        private DispatcherTimer _timerVisitors;

        // Session Phase Flags
        private bool _isLoginActive = false;
        private bool _isLectureOver = false;
        private bool _logoutEnabled = false;
        private BitmapImage? _qrCodeLogin;

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize Timers
            _timerSession = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timerSession.Tick += TimerSession_Tick;
            
            _timerVisitors = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timerVisitors.Tick += TimerVisitors_Tick;
        }

        #region UI Event Handlers

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            // Show Session Dialog
            var sessionWin = new SessionWindow();
            if (sessionWin.ShowDialog() == true && sessionWin.SelectedSession != null)
            {
                _currentSession = sessionWin.SelectedSession;
                StartServer();
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
        }
        
        private void BtnExtend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentSession == null)
                {
                    CustomMessageBox.Show("لا توجد جلسة نشطة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var inputDialog = new Window
                {
                    Title = "Extend Time",
                    Width = 380,
                    Height = 320,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent
                };

                // Main border with shadow
                var mainBorder = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(12),
                    Margin = new Thickness(15),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 20,
                        ShadowDepth = 3,
                        Opacity = 0.15,
                        Color = Colors.Black
                    }
                };

                var mainStack = new StackPanel { Margin = new Thickness(24) };

                // Title
                var titleText = new TextBlock
                {
                    Text = "Extend Session",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1f2937")),
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 6)
                };

                // Hint
                var hintText = new TextBlock
                {
                    Text = "Enter additional minutes",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af")),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 16)
                };

                // Input field container
                var inputBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f9fafb")),
                    CornerRadius = new CornerRadius(8),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e5e7eb")),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 12),
                    Padding = new Thickness(0)
                };

                var txtMinutes = new TextBox
                {
                    Text = "5",
                    FontSize = 28,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(12, 10, 12, 10),
                    TextAlignment = TextAlignment.Center,
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981")),
                    BorderThickness = new Thickness(0),
                    CaretBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981"))
                };
                inputBorder.Child = txtMinutes;
                txtMinutes.GotFocus += (s, args) => { try { txtMinutes.SelectAll(); } catch { } };

                // Error label
                var errorLabel = new TextBlock
                {
                    Text = "",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 16),
                    TextWrapping = TextWrapping.Wrap
                };

                // Buttons Grid
                var buttonsGrid = new Grid();
                buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

                var btnOk = new Button
                {
                    Content = "Extend",
                    Height = 40,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 14,
                    FontWeight = FontWeights.Medium,
                    Cursor = Cursors.Hand
                };
                btnOk.Resources.Add(typeof(Border), new Style(typeof(Border)) { Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(6)) } });
                Grid.SetColumn(btnOk, 0);

                var btnCancel = new Button
                {
                    Content = "Cancel",
                    Height = 40,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f3f4f6")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b7280")),
                    BorderThickness = new Thickness(0),
                    FontSize = 14,
                    Cursor = Cursors.Hand
                };
                btnCancel.Resources.Add(typeof(Border), new Style(typeof(Border)) { Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(6)) } });
                Grid.SetColumn(btnCancel, 2);

                buttonsGrid.Children.Add(btnOk);
                buttonsGrid.Children.Add(btnCancel);

                // Build UI
                mainStack.Children.Add(titleText);
                mainStack.Children.Add(hintText);
                mainStack.Children.Add(inputBorder);
                mainStack.Children.Add(errorLabel);
                mainStack.Children.Add(buttonsGrid);
                mainBorder.Child = mainStack;
                inputDialog.Content = mainBorder;

                // Result variable
                double extendMinutes = 0;

                // Event handlers
                btnCancel.Click += (s, args) =>
                {
                    try
                    {
                        inputDialog.DialogResult = false;
                        inputDialog.Close();
                    }
                    catch { }
                };

                btnOk.Click += (s, args) =>
                {
                    try
                    {
                        errorLabel.Text = "";
                        
                        string input = txtMinutes.Text?.Trim() ?? "";
                        input = input.Replace(",", ".").Replace(" ", "");
                        
                        if (string.IsNullOrEmpty(input))
                        {
                            errorLabel.Text = "⚠ Please enter a value";
                            return;
                        }

                        // Try parsing with invariant culture
                        if (!double.TryParse(input, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowDecimalPoint, 
                            System.Globalization.CultureInfo.InvariantCulture, out extendMinutes))
                        {
                            // Try with current culture as fallback
                            if (!double.TryParse(input, out extendMinutes))
                            {
                                errorLabel.Text = "⚠ Invalid format. Enter a number (e.g., 5 or 1.5)";
                                return;
                            }
                        }

                        if (extendMinutes <= 0)
                        {
                            errorLabel.Text = "⚠ Must be greater than 0";
                            return;
                        }

                        if (extendMinutes > 300)
                        {
                            errorLabel.Text = "⚠ Maximum is 300 minutes";
                            return;
                        }

                        if (double.IsNaN(extendMinutes) || double.IsInfinity(extendMinutes))
                        {
                            errorLabel.Text = "⚠ Invalid value";
                            return;
                        }

                        inputDialog.DialogResult = true;
                        inputDialog.Close();
                    }
                    catch (Exception ex)
                    {
                        errorLabel.Text = $"⚠ Error: {ex.Message}";
                    }
                };

                // Enter key support
                txtMinutes.KeyDown += (s, args) =>
                {
                    try
                    {
                        if (args.Key == Key.Enter)
                            btnOk.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        else if (args.Key == Key.Escape)
                            btnCancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch { }
                };

                // Focus input on load
                inputDialog.Loaded += (s, args) =>
                {
                    try
                    {
                        txtMinutes.Focus();
                        txtMinutes.SelectAll();
                    }
                    catch { }
                };

                // Show dialog
                bool? dialogResult = null;
                try
                {
                    dialogResult = inputDialog.ShowDialog();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Process result
                if (dialogResult == true && extendMinutes > 0)
                {
                    try
                    {
                        if (_currentSession == null)
                        {
                            CustomMessageBox.Show("Session ended", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        double prevDuration = _currentSession.LoginDurationMinutes;
                        _currentSession.LoginDurationMinutes += extendMinutes;

                        string message;
                        if (!_isLoginActive)
                        {
                            _isLoginActive = true;
                            message = $"تم تمديد الجلسة بنجاح\n\n⏱️ الوقت المضاف: {extendMinutes:0.##} دقيقة\n📊 الإجمالي: {_currentSession.LoginDurationMinutes:0.##} دقيقة";
                        }
                        else
                        {
                            message = $"تم تمديد الوقت بنجاح\n\n⏱️ المضاف: +{extendMinutes:0.##} دقيقة\n📊 الإجمالي الجديد: {_currentSession.LoginDurationMinutes:0.##} دقيقة";
                        }

                        CustomMessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateSessionState();
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Failed to extend: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Unexpected error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnEndLecture_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSession != null && !_isLectureOver)
            {
                _isLectureOver = true;
                _isLoginActive = false; // Stop logins
                UpdateSessionState(); // Show logout QR
                MessageBox.Show("تم إنهاء المحاضرة. يمكن للطلاب تسجيل الخروج الآن.", "إنهاء", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshGrid();
        }

        private void BtnLogoutAll_Click(object sender, RoutedEventArgs e)
        {
            // Allow enabling checkout at any time (not just after lecture ends)
            if (MessageBox.Show("Enable checkout for all students?\nThey will be able to checkout from their phones.", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _logoutEnabled = true;
                _isLectureOver = true; // Also mark lecture as over
                btnLogoutAll.IsEnabled = false;
                btnLogoutAll.Content = "✓ CHECKOUT ENABLED";
                UpdateSessionState();
                MessageBox.Show("Checkout enabled! Students can now checkout from their phones.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_students.Count == 0)
            {
                MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"Attendance_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("ID,Name,Phone,Device,CheckIn,CheckOut");
                    
                    lock (_studentsLock)
                    {
                        foreach (var s in _students)
                        {
                            sb.AppendLine($"{s.Id},{EscapeCsv(s.Name)},{EscapeCsv(s.Phone)},{EscapeCsv(s.DeviceType)},{s.RegisterTime:HH:mm:ss},{s.LogoutTime:HH:mm:ss}");
                        }
                    }
                    
                    File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                    CustomMessageBox.Show("تم التصدير بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"حدث خطأ أثناء التصدير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("هل أنت متأكد من مسح جميع البيانات؟", "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                lock (_studentsLock)
                {
                    _students.Clear();
                }
                RefreshGrid();
            }
        }

        // Theme toggle removed - keeping dark theme only

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void ImgQrLogin_Click(object sender, MouseButtonEventArgs e)
        {
            if (_qrCodeLogin != null)
            {
                var win = new Window
                {
                    Title = "Attendance QR Code",
                    Width = 450,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new System.Windows.Controls.Image { Source = _qrCodeLogin, Margin = new Thickness(20) },
                    Background = System.Windows.Media.Brushes.White
                };
                win.ShowDialog();
            }
        }

        private void BtnDiscord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://discord.gg/2zeME6Ea",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/cluckegy/QR-Login",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        #endregion

        #region Server Logic

        private void StartServer()
        {
            try
            {
                int port = 9512; // Fixed port
                int httpsPort = 443; // Standard HTTPS port
                string localIp = GetLocalIPAddress();

                if (string.IsNullOrEmpty(localIp) || localIp == "127.0.0.1")
                {
                    CustomMessageBox.Show("لم يتم العثور على عنوان IP. تأكد من الاتصال بالشبكة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _serverUrl = $"http://{localIp}:{port}/";
                AddFirewallRule(port);
                AddFirewallRule(httpsPort);

                // Generate self-signed certificate for HTTPS
                _serverCertificate = GenerateSelfSignedCertificate(localIp);

                // Start HTTP Listener
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://+:{port}/");

                _isRunning = true;
                _serverThread = new Thread(ServerLoop) { IsBackground = true };
                
                _httpListener.Start();
                _serverThread.Start();

                // Start HTTPS Listener
                try
                {
                    _httpsListener = new TcpListener(IPAddress.Any, httpsPort);
                    _httpsListener.Start();
                    _httpsThread = new Thread(HttpsServerLoop) { IsBackground = true };
                    _httpsThread.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"HTTPS failed: {ex.Message}");
                    // Continue without HTTPS - HTTP will still work
                }
                
                // Initialize Session State
                _isLoginActive = true;
                _isLectureOver = false;

                UpdateSessionState();
                UpdateUiState(true);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"فشل تشغيل السيرفر: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                StopServer();
            }
        }

        private void StopServer()
        {
            _isRunning = false;
            try { _httpListener?.Stop(); } catch { }
            try { _httpsListener?.Stop(); } catch { }
            _serverCertificate?.Dispose();
            _serverCertificate = null;
            UpdateUiState(false);
        }

        private void ServerLoop()
        {
            while (_isRunning && _httpListener != null && _httpListener.IsListening)
            {
                try
                {
                    var context = _httpListener.GetContext();
                    ThreadPool.QueueUserWorkItem(HandleRequest, context);
                }
                catch { }
            }
        }

        private void HttpsServerLoop()
        {
            while (_isRunning && _httpsListener != null)
            {
                try
                {
                    var client = _httpsListener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleHttpsClient, client);
                }
                catch { }
            }
        }

        private void HandleHttpsClient(object? state)
        {
            if (state is not TcpClient client || _serverCertificate == null) return;
            
            try
            {
                using var sslStream = new SslStream(client.GetStream(), false);
                sslStream.AuthenticateAsServer(_serverCertificate, false, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, false);
                
                // Read HTTP request
                using var reader = new StreamReader(sslStream);
                var requestLine = reader.ReadLine() ?? "";
                
                // Read headers
                string? line;
                string userAgent = "";
                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                {
                    if (line.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase))
                        userAgent = line.Substring(11).Trim();
                }

                // Parse request
                var parts = requestLine.Split(' ');
                string method = parts.Length > 0 ? parts[0] : "GET";
                string path = parts.Length > 1 ? parts[1] : "/";
                string? codeFromUrl = null;
                
                if (path.Contains("?code="))
                {
                    var match = Regex.Match(path, @"code=([^&]+)");
                    if (match.Success) codeFromUrl = match.Groups[1].Value;
                }

                // Track visitor
                string clientIp = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "unknown";
                TrackVisitor(clientIp);

                // Build response
                string responseBody;
                string contentType = "text/html";
                
                if (path.StartsWith("/api/status"))
                {
                    responseBody = JsonSerializer.Serialize(new { 
                        loginActive = _isLoginActive, 
                        lectureOver = _isLectureOver,
                        logoutEnabled = _logoutEnabled
                    });
                    contentType = "application/json";
                }
                else
                {
                    responseBody = GetStudentPage(codeFromUrl);
                }

                // Send response
                var responseBytes = Encoding.UTF8.GetBytes(responseBody);
                var httpResponse = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}; charset=utf-8\r\nContent-Length: {responseBytes.Length}\r\nAccess-Control-Allow-Origin: *\r\nConnection: close\r\n\r\n";
                var headerBytes = Encoding.UTF8.GetBytes(httpResponse);
                
                sslStream.Write(headerBytes);
                sslStream.Write(responseBytes);
                sslStream.Flush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTPS request error: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        private X509Certificate2 GenerateSelfSignedCertificate(string subjectName)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                $"CN={subjectName}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add extensions
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            
            // Add SAN (Subject Alternative Name) for the IP
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Parse(subjectName));
            sanBuilder.AddDnsName(subjectName);
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Create self-signed certificate valid for 1 year
            var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(1));

            // Export and reimport to make it usable with SslStream
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        }

        private void HandleRequest(object? state)
        {
            if (state is not HttpListenerContext context) return;

            try
            {
                var request = context.Request;
                var response = context.Response;
                string path = request.Url?.AbsolutePath.ToLower() ?? "/";
                string clientIp = request.RemoteEndPoint.Address.ToString();

                TrackVisitor(clientIp);

                byte[] buffer;
                response.ContentType = "text/html";
                response.ContentEncoding = Encoding.UTF8;
                
                // Enable CORS
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "POST, GET");

                if (path == "/api/attend" && request.HttpMethod == "POST")
                {
                    if (_isLoginActive)
                    {
                        using var reader = new StreamReader(request.InputStream);
                        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
                        
                        if (data != null && _currentSession != null && 
                            data.TryGetValue("sessionId", out string? sid) && sid == _currentSession.SessionId)
                        {
                            string device = ParseUserAgent(request.UserAgent);
                            string fingerprint = data.GetValueOrDefault("fingerprint", "");
                            bool isIncognito = data.GetValueOrDefault("incognito", "false") == "true";
                            
                            // ANTI-CHEAT: Block incognito mode
                            if (isIncognito)
                            {
                                buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = false, error = "⛔ المتصفح الخفي غير مسموح به" }));
                            }
                            // ANTI-CHEAT: Check if IP already registered
                            else if (IsIpRegistered(clientIp))
                            {
                                buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = false, error = "⛔ تم التسجيل من هذا الجهاز مسبقاً (IP)" }));
                            }
                            // ANTI-CHEAT: Check if fingerprint already registered
                            else if (!string.IsNullOrEmpty(fingerprint) && IsFingerprintRegistered(fingerprint))
                            {
                                buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = false, error = "⛔ تم التسجيل من هذا الجهاز مسبقاً" }));
                            }
                            else
                            {
                                bool isNew = RegisterStudent(data.GetValueOrDefault("name", ""), data.GetValueOrDefault("phone", ""), device, clientIp, fingerprint);
                                string msg = isNew ? "تم تسجيل حضورك بنجاح" : "تم تسجيل حضورك مسبقاً";
                                buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = true, message = msg }));
                            }
                        }
                        else
                        {
                            buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = false, error = "بيانات خاطئة" }));
                        }
                    }
                    else
                    {
                         buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = false, error = "انتهى وقت التسجيل" }));
                    }
                    response.ContentType = "application/json";
                }
                else if (path == "/api/logout" && request.HttpMethod == "POST")
                {
                     if (_isLectureOver)
                     {
                        using var reader = new StreamReader(request.InputStream);
                        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
                        
                        if (data != null && _currentSession != null && 
                            data.TryGetValue("sessionId", out string? sid) && sid == _currentSession.SessionId + "_LOGOUT")
                        {
                            bool loggedOut = LogoutStudent(data.GetValueOrDefault("phone", ""));
                            string msg = loggedOut ? "تم تسجيل خروجك بنجاح" : "لم يتم العثور على سجل الحضور";
                            buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = true, message = msg }));
                        }
                        else
                        {
                            buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = false, error = "كود الخروج خاطئ" }));
                        }
                     }
                     else
                     {
                         buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = false, error = "المحاضرة لم تنته بعد" }));
                     }
                     response.ContentType = "application/json";
                }
                else if (path == "/api/status")
                {
                    // For client polling (to enable logout button)
                    buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { 
                        loginActive = _isLoginActive, 
                        lectureOver = _isLectureOver,
                        logoutEnabled = _logoutEnabled,
                        timeLeft = _currentSession != null ? 
                            Math.Max(0, (_currentSession.LectureDurationMinutes * 60) - (DateTime.Now - _currentSession.StartTime).TotalSeconds) : 0
                    }));
                    response.ContentType = "application/json";
                }
                else if (path == "/api/page-leave" && request.HttpMethod == "POST")
                {
                    // Track when student leaves the page (possibly to use camera)
                    using var reader = new StreamReader(request.InputStream);
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
                    
                    if (data != null && data.TryGetValue("phone", out string? phone))
                    {
                        lock (_studentsLock)
                        {
                            var student = _students.FirstOrDefault(s => s.Phone == phone);
                            if (student != null)
                            {
                                student.LeftPageCount++;
                            }
                        }
                        Dispatcher.BeginInvoke(new Action(RefreshGrid));
                        buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = true }));
                    }
                    else
                    {
                        buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = false }));
                    }
                    response.ContentType = "application/json";
                }
                else
                {
                    // Extract code from URL query parameter
                    string? codeFromUrl = null;
                    if (request.Url?.Query != null)
                    {
                        var query = HttpUtility.ParseQueryString(request.Url.Query);
                        codeFromUrl = query["code"];
                    }
                    string page = GetStudentPage(codeFromUrl);
                    buffer = Encoding.UTF8.GetBytes(page);
                }

                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
            catch { }
        }

        #endregion

        #region Logic Helpers

        private bool RegisterStudent(string name, string phone, string device, string ipAddress, string fingerprint)
        {
            bool success = false;
            lock (_studentsLock)
            {
                if (_students.Any(s => s.Phone == phone)) return false;
                _students.Add(new Student
                {
                    Id = _students.Count + 1,
                    Name = name,
                    Phone = phone,
                    DeviceType = device,
                    IpAddress = ipAddress,
                    DeviceFingerprint = fingerprint,
                    RegisterTime = DateTime.Now
                });
                success = true;
            }
            if (success)
            {
                Dispatcher.BeginInvoke(new Action(RefreshGrid));
            }
            return success;
        }

        private bool IsIpRegistered(string ip)
        {
            lock (_studentsLock)
            {
                return _students.Any(s => s.IpAddress == ip);
            }
        }

        private bool IsFingerprintRegistered(string fingerprint)
        {
            lock (_studentsLock)
            {
                return _students.Any(s => s.DeviceFingerprint == fingerprint);
            }
        }

        private bool LogoutStudent(string phone)
        {
            bool success = false;
            lock (_studentsLock)
            {
                var student = _students.FirstOrDefault(s => s.Phone == phone);
                if (student != null)
                {
                    student.LogoutTime = DateTime.Now;
                    success = true;
                }
            }
            if (success)
            {
                Dispatcher.BeginInvoke(new Action(RefreshGrid));
            }
            return success;
        }

        private void UpdateSessionState()
        {
            if (_currentSession == null) return;
            
            // btnLogoutAll is now enabled during the session (controlled in UpdateUiState)
            // Only disable it if checkout was already enabled
            if (_logoutEnabled)
            {
                btnLogoutAll.IsEnabled = false;
                btnLogoutAll.Content = "✓ CHECKOUT ENABLED";
            }
            
            // Generate QR Code for login
            if (_isLoginActive)
            {
                string loginUrl = $"{_serverUrl}?code={_currentSession.SessionId}";
                _qrCodeLogin = GenerateQrBitmap(loginUrl);
                imgQrLogin.Source = _qrCodeLogin;
            }
        }

        private BitmapImage GenerateQrBitmap(string text)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var code = new QRCode(data);
            using var bitmap = code.GetGraphic(20);
            
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void UpdateUiState(bool running)
        {
            btnStart.IsEnabled = !running;
            btnStop.IsEnabled = running;
            btnExtend.IsEnabled = running;
            btnLogoutAll.IsEnabled = running && !_logoutEnabled; // Enable during session until checkout is enabled
            
            if (running)
            {
                txtSessionInfo.Text = $"{_currentSession?.Department} - {_currentSession?.GradeString}";
                
                _timerSession.Start();
                _timerVisitors.Start();
            }
            else
            {
                txtSessionInfo.Text = "No Active Session";
                txtTimer.Text = "00:00:00";
                
                _timerSession.Stop();
                _timerVisitors.Stop();
            }
        }

        private void RefreshGrid()
        {
            var studentsCopy = new List<Student>();
            lock (_studentsLock)
            {
                studentsCopy = _students.ToList();
            }
            gridStudents.ItemsSource = null;
            gridStudents.ItemsSource = studentsCopy;
            txtTotalStudents.Text = studentsCopy.Count.ToString();
        }

        private void TimerSession_Tick(object? sender, EventArgs e)
        {
            if (_currentSession != null)
            {
                var elapsed = DateTime.Now - _currentSession.StartTime;
                txtTimer.Text = elapsed.ToString(@"hh\:mm\:ss");

                // Check Login Duration
                if (_isLoginActive && elapsed.TotalMinutes >= _currentSession.LoginDurationMinutes)
                {
                    _isLoginActive = false;
                    UpdateSessionState(); // Hide login QR
                }

                // Check Lecture Duration
                if (!_isLectureOver && elapsed.TotalMinutes >= _currentSession.LectureDurationMinutes)
                {
                    _isLectureOver = true;
                    UpdateSessionState(); // Show logout QR
                }
            }
        }

        private void TimerVisitors_Tick(object? sender, EventArgs e)
        {
             lock (_visitorsLock)
            {
                var inactive = _visitorActivity.Where(k => (DateTime.Now - k.Value).TotalSeconds > 20).Select(k => k.Key).ToList();
                foreach(var k in inactive) _visitorActivity.Remove(k);
                _activeVisitors = _visitorActivity.Count;
            }
            txtActiveVisitors.Text = _activeVisitors.ToString();
        }

        private void TrackVisitor(string ip)
        {
            lock (_visitorsLock)
            {
                _visitorActivity[ip] = DateTime.Now;
            }
        }

        private string GetLocalIPAddress()
        {
            try {
                // Return Hotspot IP or first valid Local IP
                 foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                string s = ip.Address.ToString();
                                if (s.StartsWith("192.168.137.")) return s; // Hotspot
                            }
                        }
                    }
                }
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                     if (ip.AddressFamily == AddressFamily.InterNetwork) return ip.ToString();
                }
            } catch { }
            return "127.0.0.1";
        }
        
        private string ParseUserAgent(string? ua)
        {
            if (string.IsNullOrEmpty(ua)) return "Unknown";
            if (ua.Contains("Android")) return "Android";
            if (ua.Contains("iPhone") || ua.Contains("iPad")) return "iOS";
            if (ua.Contains("Windows")) return "Windows";
            return "Web";
        }

        private string EscapeCsv(string? val) => val?.Replace(",", " ") ?? "";

        private void AddFirewallRule(int port)
        {
            try 
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"AttendanceSystem_WPF\" dir=in action=allow protocol=tcp localport={port}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                System.Diagnostics.Process.Start(psi);
            } 
            catch { }
        }

        #endregion

        #region HTML Page Generator
        
        private string GetStudentPage(string? codeFromUrl = null)
        {
            string sessionId = _currentSession?.SessionId ?? "";
            string dept = HttpUtility.HtmlEncode(_currentSession?.Department ?? "");
            string grade = _currentSession?.GradeString ?? "";
            long startTime = new DateTimeOffset(_currentSession?.StartTime ?? DateTime.Now).ToUnixTimeMilliseconds();
            double lectureDuration = _currentSession?.LectureDurationMinutes ?? 60;
            string urlCode = HttpUtility.HtmlEncode(codeFromUrl ?? "");

            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <title>Attendance Portal</title>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap"" rel=""stylesheet"">
    <style>
        :root {{
            --bg: #f0f2f5;
            --card-bg: #ffffff;
            --primary: #10b981;
            --primary-hover: #059669;
            --primary-light: #f0fdf4;
            --danger: #ef4444;
            --danger-light: #fef2f2;
            --warning: #f59e0b;
            --warning-light: #fffbeb;
            --text-main: #1f2937;
            --text-muted: #6b7280;
            --text-light: #9ca3af;
            --input-bg: #f9fafb;
            --border: #e5e7eb;
        }}
        * {{ margin: 0; padding: 0; box-sizing: border-box; -webkit-tap-highlight-color: transparent; }}
        body {{
            font-family: 'Inter', -apple-system, sans-serif;
            background: var(--bg);
            color: var(--text-main);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }}
        .container {{
            width: 100%;
            max-width: 400px;
            background: var(--card-bg);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 28px 24px;
            box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05), 0 10px 20px -5px rgba(0,0,0,0.05);
        }}
        .header {{ text-align: center; margin-bottom: 24px; }}
        .header h1 {{ font-size: 22px; font-weight: 700; color: var(--text-main); margin-bottom: 8px; }}
        .department-badge {{ 
            display: inline-block; background: var(--primary-light); 
            color: var(--primary); padding: 6px 14px; border-radius: 20px; 
            font-size: 12px; font-weight: 600;
        }}
        .timer-display {{
            text-align: center; margin-bottom: 24px;
            background: var(--primary-light); padding: 16px; border-radius: 12px;
        }}
        .timer-label {{ font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; color: var(--primary); font-weight: 600; margin-bottom: 4px; }}
        .timer-digits {{ font-size: 32px; font-weight: 700; color: var(--primary); font-variant-numeric: tabular-nums; }}
        .form-group {{ margin-bottom: 14px; }}
        input {{
            width: 100%; background: var(--input-bg); border: 1px solid var(--border);
            color: var(--text-main); padding: 14px 16px; border-radius: 10px; font-size: 15px;
            font-family: inherit; transition: all 0.2s; outline: none;
        }}
        input:focus {{ border-color: var(--primary); box-shadow: 0 0 0 3px rgba(16, 185, 129, 0.15); }}
        input::placeholder {{ color: var(--text-light); }}
        .btn {{
            width: 100%; padding: 14px; border: none; border-radius: 10px;
            font-size: 15px; font-weight: 600; cursor: pointer;
            transition: all 0.2s;
        }}
        .btn:active {{ transform: scale(0.98); }}
        .btn-primary {{ background: var(--primary); color: white; }}
        .btn-primary:hover {{ background: var(--primary-hover); }}
        .btn-danger {{ background: var(--danger); color: white; }}
        .btn:disabled {{ opacity: 0.6; cursor: not-allowed; }}
        .success-state {{ text-align: center; padding: 16px 0; }}
        .icon-circle {{
            width: 60px; height: 60px; margin: 0 auto 16px;
            background: var(--primary-light); color: var(--primary);
            border-radius: 50%; display: flex; align-items: center; justify-content: center;
            font-size: 26px;
        }}
        .success-title {{ font-size: 18px; font-weight: 700; margin-bottom: 6px; color: var(--text-main); }}
        .success-desc {{ color: var(--text-muted); font-size: 14px; }}
        .status-msg {{ 
            margin-top: 14px; padding: 12px; border-radius: 10px; font-size: 13px; font-weight: 500; text-align: center;
        }}
        .status-error {{ background: var(--danger-light); color: var(--danger); }}
        .status-info {{ background: var(--primary-light); color: var(--primary); }}
        .hidden {{ display: none !important; }}
        .divider {{ height: 1px; background: var(--border); margin: 20px 0; }}
        .camera-warning {{
            background: var(--danger-light); border: 1px solid #fecaca; border-radius: 10px; 
            padding: 14px; margin: 16px 0; text-align: center;
        }}
        .camera-warning-icon {{ font-size: 28px; margin-bottom: 6px; }}
        .camera-warning-title {{ color: var(--danger); font-weight: 700; font-size: 13px; margin-bottom: 2px; }}
        .camera-warning-desc {{ color: var(--text-muted); font-size: 11px; }}
        @media (max-height: 700px) {{
            .container {{ padding: 20px; }}
            .header {{ margin-bottom: 20px; }}
            .timer-display {{ margin-bottom: 20px; padding: 14px; }}
            .timer-digits {{ font-size: 28px; }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Attendance Portal</h1>
            <span class=""department-badge"">{dept} • {grade}</span>
        </div>

        <div class=""timer-display"">
            <div class=""timer-label"">Session Time</div>
            <div id=""timer"" class=""timer-digits"">00:00:00</div>
        </div>

        <div id=""regForm"">
            <div class=""form-group"">
                <input type=""text"" id=""name"" placeholder=""Full Name"" autocomplete=""name"" required>
            </div>
            <div class=""form-group"">
                <input type=""tel"" id=""phone"" placeholder=""Phone Number"" autocomplete=""tel"" required>
            </div>
            <button id=""btnSubmit"" class=""btn btn-primary"" onclick=""submitAttendance()"">Check In Now</button>
            <div id=""codeStatus"" class=""status-msg hidden""></div>
        </div>

        <div id=""registeredView"" class=""hidden"">
            <div class=""success-state"">
                <div class=""icon-circle"">✓</div>
                <h2 class=""success-title"">You're Checked In!</h2>
                <p id=""studentName"" class=""success-desc"">Welcome to the session.</p>
            </div>

            <!-- CAMERA WARNING -->
            <div id=""cameraWarning"" class=""camera-warning"">
                <div class=""camera-warning-icon"">📵</div>
                <div class=""camera-warning-title"">CAMERA BLOCKED</div>
                <div class=""camera-warning-desc"">Do NOT leave this page or use camera until lecture ends</div>
            </div>

            <div class=""divider""></div>

            <div class=""timer-display"" style=""margin-bottom: 16px;"">
                <div class=""timer-label"">Remaining Time</div>
                <div id=""remainingTime"" class=""timer-digits"">--:--</div>
            </div>

            <div id=""waitMsg"" class=""status-msg status-info"">
                Instructor controls the checkout process. Please wait.
            </div>
            
            <button id=""btnLogout"" class=""btn btn-danger hidden"" onclick=""submitLogout()"" style=""margin-top: 16px;"">Sign Out of Session</button>
        </div>

        <div id=""result"" class=""hidden"">
            <div class=""success-state"">
                <div id=""resultIcon"" class=""icon-circle""></div>
                <h2 id=""resultMsg"" class=""success-title""></h2>
                <p id=""resultSubMsg"" class=""success-desc""></p>
            </div>
        </div>
    </div>

    <script>
        var SID = ""{sessionId}"";
        var START_TIME = {startTime};
        var LECTURE_DURATION = {lectureDuration} * 60 * 1000;
        var URL_CODE = ""{urlCode}"";
        
        // ========== CAMERA BLOCKING FEATURE ==========
        // Block camera access if student already registered in this session
        var isSessionActive = localStorage.getItem(""s_data_"" + SID) !== null;
        
        if(isSessionActive && navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {{
            var originalGetUserMedia = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
            navigator.mediaDevices.getUserMedia = function(constraints) {{
                // Check if requesting video (camera)
                if(constraints && constraints.video) {{
                    // Check server status to see if lecture is over
                    return fetch(""/api/status"")
                        .then(function(r) {{ return r.json(); }})
                        .then(function(status) {{
                            if(!status.lectureOver && !status.logoutEnabled) {{
                                // Lecture still in progress, block camera
                                alert(""⚠️ Wait for lecture to end\\n\\nYou cannot use the camera while the session is in progress."");
                                return Promise.reject(new DOMException(""Session in progress. Camera access blocked."", ""NotAllowedError""));
                            }} else {{
                                // Lecture is over, allow camera
                                return originalGetUserMedia(constraints);
                            }}
                        }})
                        .catch(function(err) {{
                            if(err instanceof DOMException) throw err;
                            // If fetch fails, block camera by default
                            alert(""⚠️ Wait for lecture to end\\n\\nCamera access is blocked during active session."");
                            return Promise.reject(new DOMException(""Session in progress. Camera access blocked."", ""NotAllowedError""));
                        }});
                }}
                return originalGetUserMedia(constraints);
            }};
        }}
        // ========== END CAMERA BLOCKING ==========
        
        function showStatus(msg, type) {{
            var el = document.getElementById(""codeStatus"");
            el.innerText = msg;
            el.className = ""status-msg status-"" + (type === 'ok' ? 'info' : type === 'warn' ? 'error' : 'info');
            el.classList.remove(""hidden"");
        }}

        var savedData = localStorage.getItem(""s_data_"" + SID);
        if(savedData) {{
            showRegisteredView(JSON.parse(savedData));
        }} else if(URL_CODE) {{
            showStatus(""Session active. Please enter details."", ""ok"");
        }} else {{
            showStatus(""Invalid or missing QR code."", ""warn"");
            document.getElementById(""btnSubmit"").disabled = true;
        }}

        setInterval(function() {{
            var elapsed = Math.floor((Date.now() - START_TIME) / 1000);
            var h = Math.floor(elapsed/3600).toString().padStart(2,""0"");
            var m = Math.floor((elapsed%3600)/60).toString().padStart(2,""0"");
            var s = (elapsed%60).toString().padStart(2,""0"");
            document.getElementById(""timer"").innerText = h + "":"" + m + "":"" + s;
            
            var remaining = Math.max(0, LECTURE_DURATION - (Date.now() - START_TIME));
            var rm = Math.floor(remaining / 60000);
            var rs = Math.floor((remaining % 60000) / 1000);
            var remEl = document.getElementById(""remainingTime"");
            if(remEl) {{
                remEl.innerText = rm.toString().padStart(2,""0"") + "":"" + rs.toString().padStart(2,""0"");
                if(remaining <= 0) remEl.innerText = ""Finished"";
            }}
        }}, 1000);

        function checkStatus() {{
            fetch(""/api/status"").then(function(r) {{ return r.json(); }}).then(function(d) {{
                if(d.logoutEnabled) {{
                    var btn = document.getElementById(""btnLogout"");
                    var msg = document.getElementById(""waitMsg"");
                    if(btn) btn.classList.remove(""hidden"");
                    if(msg) msg.classList.add(""hidden"");
                }}
            }}).catch(function() {{}});
        }}

        // ========== ANTI-CHEAT: INCOGNITO DETECTION ==========
        var IS_INCOGNITO = false;
        var DEVICE_FINGERPRINT = '';

        // Detect incognito mode using multiple methods
        function detectIncognito() {{
            return new Promise(function(resolve) {{
                var isIncognito = false;
                
                // Method 1: Check storage quota (Chrome incognito has limited quota)
                if('storage' in navigator && 'estimate' in navigator.storage) {{
                    navigator.storage.estimate().then(function(est) {{
                        if(est.quota && est.quota < 120000000) {{ // Less than 120MB suggests incognito
                            isIncognito = true;
                        }}
                    }}).catch(function(){{}});
                }}
                
                // Method 2: Check IndexedDB (fails in some incognito modes)
                try {{
                    var db = indexedDB.open('test');
                    db.onerror = function() {{ isIncognito = true; resolve(isIncognito); }};
                    db.onsuccess = function() {{
                        try {{ indexedDB.deleteDatabase('test'); }} catch(e){{}}
                    }};
                }} catch(e) {{ isIncognito = true; }}
                
                // Method 3: Check FileSystem API (Chrome)
                if(window.webkitRequestFileSystem) {{
                    window.webkitRequestFileSystem(window.TEMPORARY, 100, function(){{}}, function() {{
                        isIncognito = true;
                    }});
                }}
                
                // Method 4: Check if localStorage works differently
                try {{
                    localStorage.setItem('test_incognito', '1');
                    localStorage.removeItem('test_incognito');
                }} catch(e) {{ isIncognito = true; }}
                
                setTimeout(function() {{ resolve(isIncognito); }}, 100);
            }});
        }}

        // Generate device fingerprint
        function generateFingerprint() {{
            var fp = [];
            
            // Screen info
            fp.push(screen.width + 'x' + screen.height);
            fp.push(screen.colorDepth);
            fp.push(window.devicePixelRatio || 1);
            fp.push(screen.availWidth + 'x' + screen.availHeight);
            
            // Timezone
            fp.push(new Date().getTimezoneOffset());
            
            // Language
            fp.push(navigator.language || navigator.userLanguage || 'unknown');
            
            // Platform
            fp.push(navigator.platform || 'unknown');
            
            // Hardware concurrency (CPU cores)
            fp.push(navigator.hardwareConcurrency || 0);
            
            // Device memory
            fp.push(navigator.deviceMemory || 0);
            
            // Touch support
            fp.push('ontouchstart' in window ? 1 : 0);
            
            // Canvas fingerprint
            try {{
                var canvas = document.createElement('canvas');
                var ctx = canvas.getContext('2d');
                ctx.textBaseline = 'top';
                ctx.font = '14px Arial';
                ctx.fillText('QR Login FP 🔐', 2, 2);
                fp.push(canvas.toDataURL().slice(-50));
            }} catch(e) {{ fp.push('no-canvas'); }}
            
            // WebGL vendor
            try {{
                var canvas = document.createElement('canvas');
                var gl = canvas.getContext('webgl');
                var debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
                if(debugInfo) {{
                    fp.push(gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL));
                    fp.push(gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL));
                }}
            }} catch(e) {{ fp.push('no-webgl'); }}
            
            // Create hash
            var str = fp.join('|');
            var hash = 0;
            for(var i = 0; i < str.length; i++) {{
                var char = str.charCodeAt(i);
                hash = ((hash << 5) - hash) + char;
                hash = hash & hash;
            }}
            return 'FP' + Math.abs(hash).toString(16).toUpperCase();
        }}

        // Initialize anti-cheat
        detectIncognito().then(function(incognito) {{
            IS_INCOGNITO = incognito;
            DEVICE_FINGERPRINT = generateFingerprint();
            
            if(IS_INCOGNITO) {{
                showStatus('⛔ Incognito mode is NOT allowed!', 'warn');
                document.getElementById('btnSubmit').disabled = true;
            }}
        }});
        // ========== END ANTI-CHEAT ==========

        function submitAttendance() {{
            var name = document.getElementById(""name"").value.trim();
            var phone = document.getElementById(""phone"").value.trim();
            if(!name || !phone) {{ return; }}
            if(!URL_CODE) {{ alert(""Code missing""); return; }}
            
            // ANTI-CHEAT: Block if incognito
            if(IS_INCOGNITO) {{
                showStatus('⛔ Incognito mode is NOT allowed!', 'warn');
                return;
            }}

            var btn = document.getElementById(""btnSubmit"");
            btn.disabled = true;
            btn.innerText = ""Verifying..."";

            fetch(""/api/attend"", {{
                method: ""POST"",
                headers: {{ ""Content-Type"": ""application/json"" }},
                body: JSON.stringify({{ 
                    name: name, 
                    phone: phone, 
                    sessionId: URL_CODE,
                    fingerprint: DEVICE_FINGERPRINT,
                    incognito: IS_INCOGNITO ? 'true' : 'false'
                }})
            }}).then(function(r) {{ return r.json(); }}).then(function(d) {{
                if(d.success) {{
                    localStorage.setItem(""s_data_"" + SID, JSON.stringify({{ name: name, phone: phone }}));
                    showRegisteredView({{ name: name, phone: phone }});
                }} else {{
                    showStatus(d.error || ""Error processing request"", ""warn"");
                    btn.disabled = false;
                    btn.innerText = ""Check In Now"";
                }}
            }}).catch(function() {{
                showStatus(""Connection failed"", ""warn"");
                btn.disabled = false;
                btn.innerText = ""Check In Now"";
            }});
        }}

        function submitLogout() {{
            var saved = localStorage.getItem(""s_data_"" + SID);
            if(!saved) return;
            var data = JSON.parse(saved);
            var btn = document.getElementById(""btnLogout"");
            btn.disabled = true;
            btn.innerText = ""Signing out..."";
            
            fetch(""/api/logout"", {{
                method: ""POST"",
                headers: {{ ""Content-Type"": ""application/json"" }},
                body: JSON.stringify({{ phone: data.phone, sessionId: SID + ""_LOGOUT"" }})
            }}).then(function(r) {{ return r.json(); }}).then(function(d) {{
                if(d.success) {{
                    showResult(true, ""Session Complete"", ""You have successfully signed out."");
                    localStorage.removeItem(""s_data_"" + SID);
                }} else {{
                    alert(d.error || ""Error"");
                    btn.disabled = false;
                    btn.innerText = ""Sign Out of Session"";
                }}
            }}).catch(function() {{
                btn.disabled = false;
                btn.innerText = ""Sign Out of Session"";
            }});
        }}

        function showRegisteredView(data) {{
            document.getElementById(""regForm"").classList.add(""hidden"");
            document.getElementById(""result"").classList.add(""hidden"");
            document.getElementById(""registeredView"").classList.remove(""hidden"");
            document.getElementById(""studentName"").innerText = ""Student: "" + data.name;
        }}

        function showResult(success, msg, subMsg) {{
            document.getElementById(""regForm"").classList.add(""hidden"");
            document.getElementById(""registeredView"").classList.add(""hidden"");
            document.getElementById(""result"").classList.remove(""hidden"");
            
            var icon = document.getElementById(""resultIcon"");
            icon.innerText = success ? ""✓"" : ""!"";
            icon.style.color = success ? ""var(--success)"" : ""var(--danger)"";
            icon.style.background = success ? ""rgba(16, 185, 129, 0.1)"" : ""rgba(239, 68, 68, 0.1)"";
            icon.style.borderColor = success ? ""rgba(16, 185, 129, 0.2)"" : ""rgba(239, 68, 68, 0.2)"";
            
            document.getElementById(""resultMsg"").innerText = msg;
            document.getElementById(""resultSubMsg"").innerText = subMsg || """";
        }}

        setInterval(checkStatus, 2000);
        if(savedData) checkStatus();

        // ========== PAGE LEAVE DETECTION ==========
        // Track when student leaves the page (possibly to use camera)
        document.addEventListener(""visibilitychange"", function() {{
            if(document.visibilityState === ""hidden"") {{
                var saved = localStorage.getItem(""s_data_"" + SID);
                if(saved) {{
                    var data = JSON.parse(saved);
                    // Report to server that student left the page
                    navigator.sendBeacon(""/api/page-leave"", JSON.stringify({{ phone: data.phone }}));
                }}
            }}
        }});
        
        // Also track when page is about to unload
        window.addEventListener(""beforeunload"", function() {{
            var saved = localStorage.getItem(""s_data_"" + SID);
            if(saved) {{
                var data = JSON.parse(saved);
                navigator.sendBeacon(""/api/page-leave"", JSON.stringify({{ phone: data.phone }}));
            }}
        }});
        // ========== END PAGE LEAVE DETECTION ==========
    </script>
</body>
</html>";
        }

        #endregion
    }
}
