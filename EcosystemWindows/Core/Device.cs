using System;
using System.Net;

namespace EcosystemWindows.Core
{
    public class Device
    {
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public string Platform { get; set; } // "Windows" or "Android"
        public DeviceStatus Status { get; set; }
        public DateTime LastSeen { get; set; }

        public Device()
        {
            LastSeen = DateTime.Now;
            Status = DeviceStatus.Disconnected;
        }

        public IPEndPoint GetEndPoint()
        {
            return new IPEndPoint(IPAddress.Parse(IpAddress), Port);
        }

        public override string ToString()
        {
            return $"{Name} ({Platform}) - {IpAddress}:{Port} [{Status}]";
        }
    }

    public enum DeviceStatus
    {
        Disconnected,
        Discovered,
        Connected,
        Connecting,
        Error
    }
}