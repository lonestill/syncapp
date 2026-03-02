using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EcosystemWindows.Core
{
    public class TcpServer : IDisposable
    {
        private const int TCP_PORT = 55556;
        private TcpListener tcpListener;
        private CancellationTokenSource cancellationTokenSource;
        private Task listenTask;
        private System.Net.Sockets.TcpClient currentClient;
        private NetworkStream currentStream;
        
        public event Action<Device> ClientConnected;
        public event Action<Device> ClientDisconnected;
        public event Action<string> MessageReceived;
        public event Action<string> ConnectionError;

        private Device connectedDevice;

        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();
            tcpListener = new TcpListener(IPAddress.Any, TCP_PORT);
            tcpListener.Start();
            
            listenTask = Task.Run(() => ListenForConnections(cancellationTokenSource.Token));
        }

        private async Task ListenForConnections(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await tcpListener.AcceptTcpClientAsync();
                    if (currentClient?.Connected == true)
                    {
                        client.Close();
                        ConnectionError?.Invoke("Connection rejected: server already has an active client.");
                        continue;
                    }
                    _ = Task.Run(() => HandleClient(client, cancellationToken));
                }
                catch (Exception ex)
                {
                    ConnectionError?.Invoke($"TCP server error: {ex.Message}");
                }
            }
        }

        private async Task HandleClient(System.Net.Sockets.TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                var endPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                var stream = client.GetStream();
                
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    // Read initial connection message
                    var connectMessage = await reader.ReadLineAsync();
                    if (connectMessage?.StartsWith("CONNECT:") == true)
                    {
                        var parts = connectMessage.Split(':');
                        if (parts.Length >= 3)
                        {
                            connectedDevice = new Device
                            {
                                Name = parts[1],
                                IpAddress = endPoint.Address.ToString(),
                                Port = TCP_PORT,
                                Platform = parts[2],
                                Status = DeviceStatus.Connected
                            };

                            currentClient = client;
                            currentStream = stream;

                            // Send acceptance
                            await SendMessage($"ACCEPT:{Environment.MachineName}:Windows");
                            ClientConnected?.Invoke(connectedDevice);

                            // Handle keepalive and messages
                            await HandleClientMessages(reader, cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke($"Client handler error: {ex.Message}");
            }
            finally
            {
                client?.Close();
                if (connectedDevice != null)
                {
                    connectedDevice.Status = DeviceStatus.Disconnected;
                    ClientDisconnected?.Invoke(connectedDevice);
                    connectedDevice = null;
                    currentClient = null;
                    currentStream = null;
                }
            }
        }

        private async Task HandleClientMessages(StreamReader reader, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && currentClient?.Connected == true)
            {
                try
                {
                    var message = await reader.ReadLineAsync();
                    if (message == null) break;

                    if (message == "PING")
                    {
                        await SendMessage("PONG");
                    }
                    else if (message == "DISCONNECT")
                    {
                        break;
                    }
                    else
                    {
                        MessageReceived?.Invoke(message);
                    }
                }
                catch (IOException)
                {
                    // Client disconnected
                    break;
                }
            }
        }

        public async Task SendMessage(string message)
        {
            if (currentStream != null && currentClient?.Connected == true)
            {
                try
                {
                    var data = Encoding.UTF8.GetBytes(message + "\n");
                    await currentStream.WriteAsync(data, 0, data.Length);
                    await currentStream.FlushAsync();
                }
                catch (Exception ex)
                {
                    ConnectionError?.Invoke($"Send message error: {ex.Message}");
                }
            }
        }

        public bool IsConnected => currentClient?.Connected == true;

        public void Dispose()
        {
            cancellationTokenSource?.Cancel();
            tcpListener?.Stop();
            currentClient?.Close();
        }
    }
}