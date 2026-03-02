using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Ecosystem.Contracts;

namespace Ecosystem.Server;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new EcosystemServer(8080);
        await server.StartAsync();
    }
}

public class EcosystemServer
{
    private readonly int _port;
    private TcpListener? _listener;

    public EcosystemServer(int port)
    {
        _port = port;
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        Console.WriteLine($"Server listening on port {_port}...");

        while (true)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine($"\nClient connected: {client.Client.RemoteEndPoint}");
                
                _ = HandleClientAsync(client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            try
            {
                byte[] lengthBuffer = new byte[4];

                while (true)
                {
                    int bytesRead = await ReadExactlyAsync(stream, lengthBuffer, 4);
                    if (bytesRead == 0) break; 

                    int payloadLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
                    
                    if (payloadLength < 0 || payloadLength > 10 * 1024 * 1024) 
                    {
                        Console.WriteLine("Invalid payload length. Disconnecting client.");
                        break; 
                    }

                    byte[] payloadBuffer = new byte[payloadLength];
                    await ReadExactlyAsync(stream, payloadBuffer, payloadLength);

                    string receivedMessage = Encoding.UTF8.GetString(payloadBuffer);
                    Console.WriteLine($"Received: {receivedMessage}");

                    string ackMessage = $"ACK: Received '{receivedMessage}'";
                    byte[] ackPayload = Encoding.UTF8.GetBytes(ackMessage);
                    byte[] ackLengthBuffer = new byte[4];
                    BinaryPrimitives.WriteInt32BigEndian(ackLengthBuffer, ackPayload.Length);

                    await stream.WriteAsync(ackLengthBuffer, 0, 4);
                    await stream.WriteAsync(ackPayload, 0, ackPayload.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }
            Console.WriteLine($"Client disconnected.");
        }
    }

    private static async Task<int> ReadExactlyAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
            if (read == 0) return 0;
            totalRead += read;
        }
        return totalRead;
    }
}
