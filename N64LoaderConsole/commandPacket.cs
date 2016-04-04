using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N64LoaderConsole
{
    internal class CommandPacket
    {
        const int PACKET_SIZE = 512;
        public byte[] Packet { get; private set; }

        public enum Command : byte
        {
            Fill = 70, //char 'F'
            ReadRom = 82, //char 'R'
            StartRom = 83, //char 'S'
            TestConnection = 84, //char 'T'
            WriteRom = 87, // char 'W'

        }

        public CommandPacket(Command command)
        {
            Packet = new byte[PACKET_SIZE];
            Packet[0] = (byte)'C';
            Packet[1] = (byte)'M';
            Packet[2] = (byte)'D';
            Packet[3] = (byte)command;
        }

        public void body(byte[] content)
        {
            if (content.Length > (Packet.Length - 4))
            {
                throw new Exception("Packet size too small for body");
            }
            else
            {
                content.CopyTo(Packet, 4);
            }
        }

        public void Send(Stream stream)
        {
            if (!stream.CanWrite)
                throw new Exception("Unable to write to stream");

            stream.Write(Packet, 0, Packet.Length);
            stream.Flush();
        }

        public async Task SendAsync(Stream stream)
        {
            await stream.WriteAsync(Packet, 0, Packet.Length);
            await stream.FlushAsync();
        }
    }
}
