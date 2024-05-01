using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;

namespace VRCFTBrokenEyeModule
{
    public class Client : IDisposable
    {
        private TcpClient client = new();

        public bool Connect()
        {
            try
            {
                client.Connect("127.0.0.1", 5555);

                var stream = client.GetStream();
                var request = new byte[] { 0x00 };
                stream.Write(request);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public (bool, EyeData) FetchData()
        {
            if (client.Connected)
            {
                var stream = client.GetStream();

                var buffer = new byte[5];
                var read = stream.Read(buffer);

                var lengthBytes = buffer[1..];
                var length = BitConverter.ToUInt32(lengthBytes);

                var data = new byte[length];
                read = stream.Read(data);

                var jsonString = Encoding.UTF8.GetString(data);

                EyeData eyeDataValue = JsonSerializer.Deserialize<EyeData>(jsonString);
                return (true, eyeDataValue);
            }

            Connect(); // attempt to silently reconnect
            return (false, new EyeData());
        }

        public void Dispose()
        {
            client?.Close();
        }
    }
}