using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EcosystemWindows.Core
{
    public class DeviceManager
    {
        private readonly List<Device> devices = new List<Device>();
        private readonly object deviceLock = new object();
        private Timer keepaliveTimer;
        private readonly NetworkDiscovery networkDiscovery;
        private readonly TcpServer tcpServer;
        private TcpClient tcpClient;
        private readonly SemaphoreSlim keepaliveLock = new SemaphoreSlim(1, 1);
        private bool isConnecting;

        public event Action<Device> DeviceAdded;
        public event Action<Device> DeviceUpdated;
        public event Action<Device> DeviceRemoved;

        public DeviceManager()
        {
            // #region agent log
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sessionId = "b1bde9",
                    runId = "pre-launch",
                    hypothesisId = "H3",
                    location = "DeviceManager.cs:ctor",
                    message = "DeviceManager constructed",
                    data = new { },
                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
                });
                System.IO.File.AppendAllText("debug-b1bde9.log", json + System.Environment.NewLine);
            }
            catch
            {
            }
            // #endregion

            networkDiscovery = new NetworkDiscovery();
            tcpServer = new TcpServer();

            InitializeEventHandlers();
            StartKeepaliveTimer();
        }

        private void InitializeEventHandlers()
        {
            networkDiscovery.DeviceDiscovered += OnDeviceDiscovered;
            tcpServer.ClientConnected += OnClientConnected;
            tcpServer.ClientDisconnected += OnClientDisconnected;
            tcpServer.MessageReceived += OnMessageReceived;
            tcpServer.ConnectionError += OnConnectionError;
        }

        private void StartKeepaliveTimer()
        {
            keepaliveTimer = new Timer(async _ => await SendKeepalive(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private async Task SendKeepalive()
        {
            if (!await keepaliveLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (tcpClient?.IsConnected == true)
                {
                    await tcpClient.SendMessage("PING");
                }
                else if (tcpServer.IsConnected)
                {
                    await tcpServer.SendMessage("PING");
                }
            }
            finally
            {
                keepaliveLock.Release();
            }
        }

        private void OnDeviceDiscovered(Device device)
        {
            lock (deviceLock)
            {
                var existingDevice = devices.Find(d => IsSameEndpoint(d, device));
                if (existingDevice == null)
                {
                    devices.Add(device);
                    DeviceAdded?.Invoke(device);
                }
                else
                {
                    existingDevice.Name = device.Name;
                    existingDevice.Platform = device.Platform;
                    existingDevice.Port = device.Port;
                    existingDevice.IpAddress = device.IpAddress;
                    existingDevice.LastSeen = device.LastSeen;
                    if (existingDevice.Status != DeviceStatus.Connected)
                    {
                        existingDevice.Status = DeviceStatus.Discovered;
                    }
                    DeviceUpdated?.Invoke(existingDevice);
                }
            }
        }

        private void OnClientConnected(Device device)
        {
            lock (deviceLock)
            {
                isConnecting = false;
                var existingDevice = devices.Find(d => IsSameEndpoint(d, device));
                if (existingDevice == null)
                {
                    device.Status = DeviceStatus.Connected;
                    device.LastSeen = DateTime.Now;
                    devices.Add(device);
                    DeviceAdded?.Invoke(device);
                }
                else
                {
                    existingDevice.Status = DeviceStatus.Connected;
                    existingDevice.LastSeen = DateTime.Now;
                    DeviceUpdated?.Invoke(existingDevice);
                }
            }
        }

        private void OnClientDisconnected(Device device)
        {
            lock (deviceLock)
            {
                var existingDevice = devices.Find(d => IsSameEndpoint(d, device));
                if (existingDevice != null)
                {
                    existingDevice.Status = DeviceStatus.Disconnected;
                    existingDevice.LastSeen = DateTime.Now;
                    DeviceUpdated?.Invoke(existingDevice);
                }
            }
        }

        private void OnMessageReceived(string message)
        {
            // Handle incoming messages
            Console.WriteLine($"Message received: {message}");
        }

        private void OnConnectionError(string error)
        {
            Console.WriteLine($"Connection error: {error}");
            lock (deviceLock)
            {
                isConnecting = false;
                var connecting = devices.FirstOrDefault(d => d.Status == DeviceStatus.Connecting);
                if (connecting != null)
                {
                    connecting.Status = DeviceStatus.Error;
                    connecting.LastSeen = DateTime.Now;
                    DeviceUpdated?.Invoke(connecting);
                }
            }
        }

        public void Start()
        {
            networkDiscovery.Start();
            networkDiscovery.RegisterService();
            tcpServer.Start();
        }

        public async Task ConnectToDevice(Device device)
        {
            if (device == null || string.IsNullOrWhiteSpace(device.IpAddress) || device.Port <= 0)
            {
                return;
            }

            lock (deviceLock)
            {
                if (isConnecting)
                {
                    return;
                }
                isConnecting = true;
                device.Status = DeviceStatus.Connecting;
                DeviceUpdated?.Invoke(device);
            }

            tcpClient?.Dispose();
            tcpClient = new TcpClient();
            tcpClient.Connected += () => OnClientConnected(device);
            tcpClient.Disconnected += () => OnClientDisconnected(device);
            tcpClient.MessageReceived += OnMessageReceived;
            tcpClient.ConnectionError += OnConnectionError;
            await tcpClient.ConnectAsync(device);
        }

        private static bool IsSameEndpoint(Device a, Device b)
        {
            return string.Equals(a.IpAddress, b.IpAddress, StringComparison.OrdinalIgnoreCase)
                   && a.Port == b.Port;
        }

        public List<Device> GetDevices()
        {
            lock (deviceLock)
            {
                return new List<Device>(devices);
            }
        }

        public Device GetConnectedDevice()
        {
            lock (deviceLock)
            {
                return devices.FirstOrDefault(d => d.Status == DeviceStatus.Connected);
            }
        }

        public void CleanupInactiveDevices()
        {
            lock (deviceLock)
            {
                var inactiveDevices = devices.Where(d => 
                    DateTime.Now - d.LastSeen > TimeSpan.FromMinutes(2) && 
                    d.Status != DeviceStatus.Connected).ToList();

                foreach (var device in inactiveDevices)
                {
                    devices.Remove(device);
                    DeviceRemoved?.Invoke(device);
                }
            }
        }

        public void Dispose()
        {
            keepaliveTimer?.Dispose();
            keepaliveLock.Dispose();
            networkDiscovery?.Dispose();
            tcpServer?.Dispose();
            tcpClient?.Dispose();
        }
    }
}