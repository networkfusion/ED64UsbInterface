using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RJCP.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N64LoaderConsole
{
    internal class Program
    {
        private static SerialPortStream port = new SerialPortStream();
        private static bool connected = false;
        private static byte[] sendBuffer = new byte[512];
        private static byte[] receiveBuffer = new byte[512];

        private static void Main(string[] args)
        {


            Console.WriteLine("**********************************");
            Console.WriteLine("ED64 usb ROM loader v1.0");
            Console.WriteLine("**********************************");

            InitialiseSerialPort();

            try
            {
                if (connected == false && port != null && port.IsOpen)
                {
                    port.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("error: " + ex.ToString());
            }
            if (connected == false)
            {
                Console.WriteLine("ED64's usb port not detected");
                return;
            }
            if (args.Length != 0)
            {
                if (!string.IsNullOrEmpty(args[0]))
                {
                    Console.WriteLine("No ROM specified, exiting");
                    WriteRom(args[0]);


                    var startPacket = new CommandPacket(CommandPacket.Command.StartRom, 508);
                    startPacket.Send(port);
                }
            }

            port.Close();
        }

        private static void InitialiseSerialPort()
        {
            string[] portNames = SerialPortStream.GetPortNames();
            var testPacket = new CommandPacket(CommandPacket.Command.TestConnection,508);

            for (int i = 0; i < portNames.Length; i++)
            {
                try
                {
                    port = new SerialPortStream(portNames[i]);
                    port.WriteTimeout = 500;
                    port.ReadTimeout = 500;
                    port.Open();
                    testPacket.Send(port);

                    port.Read(receiveBuffer, 0 , 512); //should be 4 if not 512
                    //ReadResponse(port, 4); //expect RSPk

                    if (receiveBuffer[3] != 107) //k
                    {
                        port.Close();
                        throw new Exception("no response form " + portNames[i]);
                    }
                    Debug.WriteLine("ED64 port: " + portNames[i]);
                    connected = true;
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("error: " + ex.ToString());
                }
            }
        }

        public static void WriteRom(string fileName)
        {
            FileStream fileStream = File.OpenRead(fileName); //TODO: should be enclosed in a using statement
            int fileLength = (int)fileStream.Length;
            if ((long)(fileLength / 65536 * 65536) != fileStream.Length)
            {
                fileLength = (int)(fileStream.Length / 65536L * 65536L + 65536L);
            }
            byte[] fileArray = new byte[fileLength];
            fileStream.Read(fileArray, 0, (int)fileStream.Length);
            fileStream.Close();
            Console.WriteLine("len: " + fileLength);

            if (fileLength < 2097152) //0x200000 //needs filling
            {
                var fillPacket = new CommandPacket(CommandPacket.Command.Fill, 508);
                fillPacket.Send(port);


                port.Read(receiveBuffer, 0, 512);

                if (receiveBuffer[3] != 107)
                {
                    Console.WriteLine("fill error");
                    port.Close();
                    return;
                }
                Console.WriteLine("fill ok");
            }

            var writePacket = new CommandPacket(CommandPacket.Command.WriteRom, 508); //4 if not 508

            var commandInfo = new byte[4];
            commandInfo[0] = 0; //offset
            commandInfo[1] = 0; //offset
            commandInfo[2] = (byte)(fileArray.Length / 512 >> 8);
            commandInfo[3] = (byte)(fileArray.Length / 512);

            writePacket.body(commandInfo);
            writePacket.Send(port);

            Console.WriteLine("sending...");
            DateTime now = DateTime.Now;
            for (int l = 0; l < fileArray.Length; l += 32768)
            {
                if (l == 33554432)
                {
                    Console.WriteLine("next 32m!!!!!!!!!!!!!!!");

                    commandInfo[0] = 64; //0x40 - 32mb offset
                    commandInfo[1] = 0;
                    commandInfo[2] = (byte)((fileArray.Length - 33554432) / 512 >> 8);
                    commandInfo[3] = (byte)((fileArray.Length - 33554432) / 512);
                    writePacket.body(commandInfo);
                    writePacket.Send(port);
                }
                port.Write(fileArray, l, 32768);
                if (l % 524288 == 0)
                {
                    Console.WriteLine("sent: " + l);
                }
            }
            Console.WriteLine("sent: " + fileArray.Length);
            Console.WriteLine("time: " + (DateTime.Now - now).Ticks / 10000L);
            ushort crc = 0;
            for (int m = 0; m < fileArray.Length; m++)
            {
                crc += (ushort)fileArray[m];
            }
            Console.WriteLine("crc: " + crc);
        }

        private static void ReadResponse(Stream stream, int length)
        {
            var bytesRead = stream.Read(receiveBuffer, 0, length);
        }

        private static async Task ReadResponseAsync(Stream stream, int length)
        {
            await stream.ReadAsync(receiveBuffer, 0, length);
        }
    }
}
