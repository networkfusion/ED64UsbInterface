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
                if (InitialiseSerialPort())
                {
                    Console.WriteLine("Writing ROM...");
                    //todo: detect file type is correct, if not convert
                    WriteRom(args[0].ToString());

                    Thread.Sleep(1000); //TODO: testing to see if the rom image is not ready, remove if possible!
                    var startPacket = new CommandPacket(CommandPacket.Command.StartRom, 508);
                    startPacket.Send(IoPort);                    
                }
                else
                {
                    Console.WriteLine("No response received");
                }

            }
            else
            {
                //todo: try reading a rom...
                Console.WriteLine(@"No ROM specified, e.g. ""loader.exe c:\mycart.v64"".");
            }

            if (IoPort.IsOpen)
            {
                IoPort.Close();
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();
        }

        ~Program()
        {
            if (IoPort.IsOpen)
            {
                IoPort.Close();
            }
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

                    if (receiveBuffer[3] == 107) //k
                    {
                        Console.WriteLine("Connected to EverDrive64 on port: {0}", device.nodeComportName);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error: {0}", ex.ToString());
                }
            }
            return false;
        }

        public static void WriteRom(string fileName)
        {
            int fileLength = 0;
            byte[] fileArray;

            using (FileStream fileStream = File.OpenRead(fileName))
            {
                fileLength = (int)fileStream.Length;
                if ((long)(fileLength / 0x10000 * 0x10000) != fileStream.Length) //65536
                {
                    fileLength = (int)(fileStream.Length / 0x10000 * 0x10000 + 0x10000);
                }
                fileArray = new byte[fileLength];
                fileStream.Read(fileArray, 0, (int)fileStream.Length);
            }
            Console.WriteLine("File Size: " + fileLength);

            if (fileLength < 0x200000) //2097152 //needs filling
            {
                Console.WriteLine("Generating space for ROM");
                var fillPacket = new CommandPacket(CommandPacket.Command.Fill, 508);
                fillPacket.Send(IoPort);


                IoPort.Read(receiveBuffer, 0, 512);

                if (receiveBuffer[3] != 107)
                {
                    Console.WriteLine("Error generating space required, exiting");
                    return;
                }
                else
                {
                    Console.WriteLine("ROM space generated");
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

            Console.WriteLine("Sending ROM...");
            DateTime now = DateTime.Now;
            for (int l = 0; l < fileArray.Length; l += 0x8000) //32768 (256KB)
            {
                if (l == 0x2000000) // 33554432 (32MB)
                {
                    Console.WriteLine("Sending next 32MB chunk");

                    commandInfo[0] = 0x40; //64 - 32MB offset
                    commandInfo[1] = 0;
                    commandInfo[2] = (byte)((fileArray.Length - 0x2000000) / 512 >> 8);
                    commandInfo[3] = (byte)((fileArray.Length - 0x2000000) / 512);
                    writePacket.body(commandInfo);
                    writePacket.Send(IoPort);
                }

                IoPort.Write(fileArray, l, 0x8000); //32768 (256KB)

                if (l % 0x80000 == 0) //524288 (512KB)
                {
                    Console.WriteLine("sent 512KB chunk: {0} of {1}", l, fileArray.Length);
                }
            }
            Console.WriteLine("sent file: {0}", fileArray.Length);
            Console.WriteLine("elapsed time: {0}", (DateTime.Now - now).Ticks / 10000L);

            //ushort crc = 0;
            //foreach (byte b in fileArray)
            //{
            //    crc += (ushort)b;
            //}
            //Console.WriteLine("crc: " + crc);
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
