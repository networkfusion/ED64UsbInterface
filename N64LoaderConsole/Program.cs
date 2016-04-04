using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RJCP.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace N64LoaderConsole
{
    internal class Program
    {
        private static SerialPortStream IoPort = new SerialPortStream();
        private static byte[] sendBuffer = new byte[512];
        private static byte[] receiveBuffer = new byte[512];

        private static void Main(string[] args)
        {
            Console.WriteLine("**********************************");
            Console.WriteLine("EverDrive64 USB ROM Loader V1.0");
            Console.WriteLine("**********************************");
            if (args.Length != 0 && !string.IsNullOrEmpty(args[0]))
            {
                var connected = InitialiseSerialPort();

                if (connected)
                {
                    Console.WriteLine("Writing ROM...");
                    //todo: detect file type is correct, if not convert
                    WriteRom(args[0]);


                    var startPacket = new CommandPacket(CommandPacket.Command.StartRom, 508);
                    startPacket.Send(IoPort);

                    IoPort.Close();
                }
                else
                {
                    Console.WriteLine("ED64's usb port not detected");
                }

            }
            else
            {
                //todo: try reading a rom...
                Console.WriteLine(@"No ROM specified, e.g. ""loader.exe c:\mycart.v64"".");
            }

            Console.WriteLine(@"Press any key to exit.");
            Console.ReadLine();
        }

        private static bool InitialiseSerialPort()
        {            
            Console.WriteLine("Waiting for Everdrive64 USB to be connected");
            while (FindDevice.FindFdtiUsbDevices().Where(p => p.nodeDescription == "FT245R USB FIFO").Count() == 0)
            {
                Thread.Sleep(100);
            }

            var testPacket = new CommandPacket(CommandPacket.Command.TestConnection, 508);
            foreach (var device in FindDevice.FindFdtiUsbDevices().Where(p => p.nodeDescription == "FT245R USB FIFO"))
            {
                try
                {
                    IoPort = new SerialPortStream(device.nodeComportName);
                    IoPort.WriteTimeout = 500;
                    IoPort.ReadTimeout = 500;
                    IoPort.Open();
                    testPacket.Send(IoPort);

                    IoPort.Read(receiveBuffer, 0 , 512); //should be 4 if not 512
                    //ReadResponse(port, 4); //expect RSPk

                    if (receiveBuffer[3] != 107) //k
                    {
                        IoPort.Close();
                        //throw new Exception("no response form " + device.nodeDescription);
                    }
                    else
                    {
                        Console.WriteLine("Connected to EverDrive64 on port: " + device.nodeComportName);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error: " + ex.ToString());
                    Console.ReadLine();
                }
            }
            return false;
        }

        public static void WriteRom(string fileName)
        {
            int fileLength;
            byte[] fileArray;

            using (FileStream fileStream = File.OpenRead(fileName))
            {
                fileLength = (int)fileStream.Length;
                if ((long)(fileLength / 65536 * 65536) != fileStream.Length)
                {
                    fileLength = (int)(fileStream.Length / 65536L * 65536L + 65536L);
                }
                fileArray = new byte[fileLength];
                fileStream.Read(fileArray, 0, (int)fileStream.Length);
            }
            Console.WriteLine("File Size: " + fileLength);

            if (fileLength < 2097152) //0x200000 //needs filling
            {
                var fillPacket = new CommandPacket(CommandPacket.Command.Fill, 508);
                fillPacket.Send(IoPort);


                IoPort.Read(receiveBuffer, 0, 512);

                if (receiveBuffer[3] != 107)
                {
                    Console.WriteLine("fill error");
                    return;
                }
                else
                {
                    Console.WriteLine("fill ok");
                }
            }

            var writePacket = new CommandPacket(CommandPacket.Command.WriteRom, 508); //4 if not 508

            var commandInfo = new byte[4];
            commandInfo[0] = 0; //offset
            commandInfo[1] = 0; //offset
            commandInfo[2] = (byte)(fileArray.Length / 512 >> 8);
            commandInfo[3] = (byte)(fileArray.Length / 512);

            writePacket.body(commandInfo);
            writePacket.Send(IoPort);

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
                    writePacket.Send(IoPort);
                }
                IoPort.Write(fileArray, l, 32768);
                if (l % 524288 == 0)
                {
                    Console.WriteLine("sent: " + l);
                }
            }
            Console.WriteLine("sent: " + fileArray.Length);
            Console.WriteLine("time: " + (DateTime.Now - now).Ticks / 10000L);

            ushort crc = 0;
            foreach (byte b in fileArray)
            {
                crc += (ushort)b;
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
