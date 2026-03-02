using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EcosystemWindows.Core;
using Microsoft.Toolkit.Uwp.Notifications;

namespace EcosystemWindows
{
    public partial class MainWindow : Window
    {
        private DeviceManager deviceManager;
        private QrWindow qrWindow;

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

        public MainWindow()
        {
            AgentLog("MainWindow.xaml.cs:ctor:begin", "MainWindow constructor entered", "H1");
            InitializeComponent();
            AgentLog("MainWindow.xaml.cs:ctor:afterInitialize", "InitializeComponent completed", "H1");
            InitializeDeviceManager();
            AgentLog("MainWindow.xaml.cs:ctor:afterDeviceManager", "DeviceManager initialized", "H3");
            UpdateStatus(false);
        }

        private void InitializeDeviceManager()
        {
            deviceManager = new DeviceManager();
            deviceManager.DeviceAdded += OnDeviceAdded;
            deviceManager.DeviceUpdated += OnDeviceUpdated;
            deviceManager.DeviceRemoved += OnDeviceRemoved;
            deviceManager.Start();

            AddLogEntry("Application started - Device discovery initialized");
        }

        private void OnDeviceAdded(Device device)
        {
            Dispatcher.BeginInvoke(() =>
            {
                RefreshDeviceList();
                AddLogEntry($"Device discovered: {device.Name} ({device.IpAddress})");
                
                ShowNotification("Device Discovered", $"{device.Name} is now available");
            });
        }

        private void OnDeviceUpdated(Device device)
        {
            Dispatcher.BeginInvoke(() =>
            {
                RefreshDeviceList();
                UpdateStatus(device.Status == DeviceStatus.Connected);
                
                if (device.Status == DeviceStatus.Connected)
                {
                    AddLogEntry($"Connected to {device.Name}");
                    ShowNotification("Connected", $"Successfully connected to {device.Name}");
                }
                else if (device.Status == DeviceStatus.Disconnected)
                {
                    AddLogEntry($"Disconnected from {device.Name}");
                    ShowNotification("Disconnected", $"Connection to {device.Name} lost");
                }
                else if (device.Status == DeviceStatus.Error)
                {
                    AddLogEntry($"Connection error with {device.Name}");
                }
            });
        }

        private void OnDeviceRemoved(Device device)
        {
            Dispatcher.BeginInvoke(() =>
            {
                RefreshDeviceList();
                AddLogEntry($"Device removed: {device.Name}");
            });
        }

        private void RefreshDeviceList()
        {
            var devices = deviceManager.GetDevices();
            DeviceListView.ItemsSource = devices.Select(d => new
            {
                d.Name,
                d.IpAddress,
                d.Platform,
                d.Status,
                d.LastSeen,
                LastSeenFormatted = d.LastSeen.ToString("HH:mm:ss"),
                StatusColor = GetStatusColor(d.Status),
                CanConnect =
                    !string.IsNullOrWhiteSpace(d.IpAddress) &&
                    d.Port > 0 &&
                    !string.Equals(d.Platform, "Android", StringComparison.OrdinalIgnoreCase) &&
                    (d.Status == DeviceStatus.Discovered || d.Status == DeviceStatus.Disconnected),
                Device = d
            }).ToList();
        }

        private string GetStatusColor(DeviceStatus status)
        {
            return status switch
            {
                DeviceStatus.Connected => "#4CAF50",
                DeviceStatus.Discovered => "#2196F3",
                DeviceStatus.Connecting => "#FF9800",
                DeviceStatus.Error => "#F44336",
                _ => "#9E9E9E"
            };
        }

        private void UpdateStatus(bool connected)
        {
            StatusText.Text = connected ? "Connected" : "Disconnected";
            StatusText.Background = connected ? 
                System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.LightGray;
        }

        private void AddLogEntry(string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogTextBox.AppendText($"[{timestamp}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void ShowNotification(string title, string message)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .Show();
            }
            catch
            {
                // Fallback if notifications fail
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Device device)
            {
                try
                {
                    AddLogEntry($"Connecting to {device.Name} ({device.IpAddress}:{device.Port})...");
                    device.Status = DeviceStatus.Connecting;
                    RefreshDeviceList();
                    await deviceManager.ConnectToDevice(device);
                }
                catch (Exception ex)
                {
                    device.Status = DeviceStatus.Error;
                    RefreshDeviceList();
                    AddLogEntry($"Connection failed: {ex.Message}");
                }
            }
        }

        private void ShowQrButton_Click(object sender, RoutedEventArgs e)
        {
            if (qrWindow == null || !qrWindow.IsVisible)
            {
                qrWindow = new QrWindow();
                qrWindow.Show();
            }
            else
            {
                qrWindow.Activate();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDeviceList();
            deviceManager.CleanupInactiveDevices();
            AddLogEntry("Device list refreshed");
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            deviceManager?.Dispose();
            qrWindow?.Close();
        }
    }
}