using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace EcosystemWindows
{
    public partial class QrWindow : Window
    {
        public QrWindow()
        {
            InitializeComponent();
            GenerateQrCode();
        }

        private void GenerateQrCode()
        {
            try
            {
                var localIp = GetLocalIPAddress();
                var deviceName = Environment.MachineName;
                var connectionString = $"{localIp}:55556:{deviceName}";

                var qrGenerator = new QRCodeGenerator();
                var qrData = qrGenerator.CreateQrCode(connectionString, QRCodeGenerator.ECCLevel.Q);

                var modules = qrData.ModuleMatrix;
                int count = modules.Count;
                int scale = 6;
                int size = count * scale;

                var wb = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
                var pixels = new byte[size * size * 4];

                for (int y = 0; y < count; y++)
                {
                    for (int x = 0; x < count; x++)
                    {
                        bool dark = modules[y][x];
                        byte c = dark ? (byte)0 : (byte)255;
                        for (int py = 0; py < scale; py++)
                        {
                            for (int px = 0; px < scale; px++)
                            {
                                int i = ((y * scale + py) * size + (x * scale + px)) * 4;
                                pixels[i] = c;
                                pixels[i + 1] = c;
                                pixels[i + 2] = c;
                                pixels[i + 3] = 255;
                            }
                        }
                    }
                }

                wb.WritePixels(new System.Windows.Int32Rect(0, 0, size, size), pixels, size * 4, 0);
                QrCodeImage.Source = wb;

                IpAddressText.Text = $"IP Address: {localIp}";
                DeviceNameText.Text = $"Device Name: {deviceName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate QR code: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetLocalIPAddress()
        {
            var skip = new[] { "vethernet", "wsl", "docker", "vmware", "virtualbox", "hyper-v", "vswitch", "loopback" };
            try
            {
                var best = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                && !skip.Any(s => n.Description.ToLower().Contains(s))
                                && !skip.Any(s => n.Name.ToLower().Contains(s)))
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                && !IPAddress.IsLoopback(a.Address))
                    .Select(a => a.Address.ToString())
                    .FirstOrDefault();

                if (best != null) return best;
            }
            catch { }

            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
