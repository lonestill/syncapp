using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Makaretu.Dns;

namespace EcosystemWindows.Core
{
    public class NetworkDiscovery : IDisposable
    {
        private const int UDP_PORT = 55555;
        private const string SERVICE_NAME = "_myecosystem._tcp";

        private UdpClient udpClient;
        private ServiceDiscovery mdns;
        private CancellationTokenSource cancellationTokenSource;
        private Task listenTask;

        public event Action<Device> DeviceDiscovered;
        public event Action<Device> DeviceLost;

        private readonly List<Device> discoveredDevices = new List<Device>();
        private readonly object deviceLock = new object();
        private readonly string localIp;
        private readonly string localName;

        public NetworkDiscovery()
        {
            localIp = GetLocalIPAddress();
            localName = Environment.MachineName;
            InitializeUdpClient();
            InitializeMDns();
        }

        private void InitializeUdpClient()
        {
            udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, UDP_PORT));
        }

        private void InitializeMDns()
        {
            mdns = new ServiceDiscovery();
            mdns.ServiceInstanceDiscovered += OnMdnsServiceInstanceDiscovered;
        }

        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();

            // We only listen for UDP HELLO packets.
            // Broadcasting here causes self-discovery noise and duplicate entries.
            listenTask = Task.Run(() => ListenForBroadcasts(cancellationTokenSource.Token));

            mdns.QueryServiceInstances(SERVICE_NAME);
        }

        private async Task ListenForBroadcasts(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await udpClient.ReceiveAsync();
                    string message = Encoding.UTF8.GetString(result.Buffer);
                    
                    if (message.StartsWith("HELLO:"))
                    {
                        var parts = message.Split(':');
                        if (parts.Length >= 4)
                        {
                            var name = parts[1];
                            var ip = parts[2];
                            var port = int.Parse(parts[3]);
                            var platform = parts.Length >= 5 ? parts[4] : "Android";

                            // Drop our own advertisements.
                            if (string.Equals(ip, localIp, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(name, localName, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var device = new Device
                            {
                                Name = name,
                                IpAddress = ip,
                                Port = port,
                                Platform = platform,
                                Status = DeviceStatus.Discovered,
                                LastSeen = DateTime.Now
                            };

                            lock (deviceLock)
                            {
                                var existingDevice = discoveredDevices.Find(d =>
                                    string.Equals(d.IpAddress, device.IpAddress, StringComparison.OrdinalIgnoreCase) &&
                                    d.Port == device.Port &&
                                    string.Equals(d.Platform, device.Platform, StringComparison.OrdinalIgnoreCase));
                                if (existingDevice != null)
                                {
                                    existingDevice.LastSeen = DateTime.Now;
                                    existingDevice.Status = DeviceStatus.Discovered;
                                }
                                else
                                {
                                    discoveredDevices.Add(device);
                                    DeviceDiscovered?.Invoke(device);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue listening
                    Console.WriteLine($"UDP listen error: {ex.Message}");
                }
            }
        }

        private void OnMdnsServiceInstanceDiscovered(object sender, ServiceInstanceDiscoveryEventArgs e)
        {
            var name = e?.ServiceInstanceName?.Labels?.FirstOrDefault() ?? "Unknown";
            if (string.Equals(name, localName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        public void RegisterService()
        {
            var service = new ServiceProfile(
                instanceName: Environment.MachineName,
                serviceName: SERVICE_NAME,
                port: (ushort)55556,
                addresses: new[] { IPAddress.Parse(localIp) }
            );
            
            mdns.Advertise(service);
        }

        private string GetLocalIPAddress()
        {
            var skip = new[] { "vethernet", "wsl", "docker", "vmware", "virtualbox", "hyper-v", "vswitch", "loopback" };
            var best = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && !skip.Any(s => n.Description.ToLower().Contains(s))
                            && !skip.Any(s => n.Name.ToLower().Contains(s)))
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address));
            if (best != null)
            {
                return best.Address.ToString();
            }

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) return ip.ToString();
            }
            return "127.0.0.1";
        }

        public void Dispose()
        {
            cancellationTokenSource?.Cancel();
            udpClient?.Close();
            mdns?.Dispose();
        }
    }
}