using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EcosystemWindows.Core
{
    public class TcpClient : IDisposable
    {
        private System.Net.Sockets.TcpClient tcpClient;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
        private CancellationTokenSource cancellationTokenSource;
        private Task receiveTask;
        
        public event Action Connected;
        public event Action Disconnected;
        public event Action<string> MessageReceived;
        public event Action<string> ConnectionError;

        private Device targetDevice;
        private int reconnectAttempts = 0;
        private const int MAX_RECONNECT_ATTEMPTS = 3;

        public async Task ConnectAsync(Device device)
        {
            targetDevice = device;
            reconnectAttempts = 0;
            
            await AttemptConnection();
        }

        private async Task AttemptConnection()
        {
            try
            {
                tcpClient = new System.Net.Sockets.TcpClient();
                var connectTask = tcpClient.ConnectAsync(targetDevice.IpAddress, targetDevice.Port);
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                    throw new TimeoutException("Connection timed out (5s)");
                await connectTask;
                stream = tcpClient.GetStream();
                reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true);
                writer = new StreamWriter(stream, Encoding.UTF8, 1024, true)
                {
                    AutoFlush = true
                };

                // Send connection message
                await SendMessage($"CONNECT:{Environment.MachineName}:Windows");

                // Wait for acceptance
                var response = await reader.ReadLineAsync();
                if (response?.StartsWith("ACCEPT:") == true)
                {
                    targetDevice.Status = DeviceStatus.Connected;
                    Connected?.Invoke();

                    // Start receiving messages
                    cancellationTokenSource = new CancellationTokenSource();
                    receiveTask = Task.Run(() => ReceiveMessages(cancellationTokenSource.Token));
                }
                else
                {
                    throw new Exception("Connection rejected");
                }
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke($"Connection failed: {ex.Message}");
                targetDevice.Status = DeviceStatus.Error;
                await HandleReconnection();
            }
        }

        private async Task ReceiveMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
            {
                try
                {
                    var message = await reader.ReadLineAsync();
                    if (message == null) break;

                    if (message == "PING")
                    {
                        await SendMessage("PONG");
                    }
                    else if (message == "PONG")
                    {
                        // Keepalive response
                    }
                    else
                    {
                        MessageReceived?.Invoke(message);
                    }
                }
                catch (IOException)
                {
                    break;
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await HandleDisconnection();
            }
        }

        public async Task SendMessage(string message)
        {
            if (writer != null && tcpClient?.Connected == true)
            {
                try
                {
                    await writer.WriteLineAsync(message);
                }
                catch (Exception ex)
                {
                    ConnectionError?.Invoke($"Send message error: {ex.Message}");
                }
            }
        }

        private async Task HandleDisconnection()
        {
            targetDevice.Status = DeviceStatus.Disconnected;
            Disconnected?.Invoke();
            await HandleReconnection();
        }

        private async Task HandleReconnection()
        {
            if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
            {
                reconnectAttempts++;
                await Task.Delay(2000); // Wait 2 seconds before reconnection attempt
                await AttemptConnection();
            }
        }

        public bool IsConnected => tcpClient?.Connected == true;

        public void Dispose()
        {
            cancellationTokenSource?.Cancel();
            writer?.Dispose();
            reader?.Dispose();
            stream?.Close();
            tcpClient?.Close();
        }
    }
}