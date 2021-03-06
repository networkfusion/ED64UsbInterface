﻿using System;
using System.IO;
using RJCP.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection;

namespace N64LoaderConsole
{
    internal class Program
    {
        const int CHUNK_32KB = 0x8000; //32768
        const int CHUNK_64KB = 0x10000; //65536
        const int CHUNK_512KB = 0x80000; //524288
        const int CHUNK_2MB = 0x200000;//2097152
        const int CHUNK_32MB = 0x2000000; //33554432

        private static SerialPortStream IoPort = new SerialPortStream();
        private static byte[] sendBuffer = new byte[512];
        private static byte[] receiveBuffer = new byte[512];

        private static void Main(string[] args)
        {
            Console.WriteLine("****************************************");
            Console.WriteLine("EverDrive64 USB Tools V{0}", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine("****************************************");
            if (args.Length != 0 && !string.IsNullOrEmpty(args[0]))
            {
                if (InitializeSerialPort())
                {
                    Console.WriteLine("Preparing ROM for flash cart...");
                    //TODO: detect file type is correct, if not convert

                    //var file = RomConverter.RomConverter.ConvertFile(args[0].ToString(), RomConverter.RomConverter.RomType.v64);
                    var file = args[0].ToString();

                    if (!string.IsNullOrEmpty(file))
                    {
                        WriteRom(file);

                        Console.WriteLine("Booting ROM on flash cart...");
                        var startPacket = new CommandPacket(CommandPacket.Command.StartRom);
                        startPacket.Send(IoPort);
                    }
                    else
                    {
                        Console.WriteLine("File conversion failed");
                    }
                }
                else
                {
                    Console.WriteLine("No response received");
                }

            }
            else
            {
                if (InitializeSerialPort())
                {
                    Console.WriteLine(@"Menu:");
                    Console.WriteLine(@"1) Take a screenshot.");
                    Console.WriteLine(@"2) Copy ROM to ED64 and Start ROM");
                    Console.WriteLine(@"'E') Exit");
                    Console.WriteLine(@"'?') Help");
                    var exit = false;
                    while (!exit)
                    {
                        var key = Console.ReadKey();
                        switch (key.KeyChar)
                        {
                            case '1':
                                Screenshot.TakeScreenshot(IoPort, $"screenshot-{DateTime.Now.ToString("yyyy-dd-MM-HH-mm-ss")}.bmp");
                                break;
                            case '2':
                                Console.WriteLine("Enter full path to ROM:");
                                var file = Console.ReadLine();
                                WriteRom(file);
                                Console.WriteLine("Booting ROM on flash cart...");
                                var startPacket = new CommandPacket(CommandPacket.Command.StartRom);
                                startPacket.Send(IoPort);
                                break;
                            case 'E':
                                exit = true;
                                break;
                            case '?':
                                Console.WriteLine(@"Send and start a ROM through the commandline e.g. ""loader.exe c:\mycart.v64"".");
                                break;
                            default:
                                Console.WriteLine("Invalid Input...");
                                break;
                        }
                    }

                }
            
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

        private static bool InitializeSerialPort()
        {            
            Console.WriteLine("Waiting for Everdrive64 USB to be connected");
            while (FindDevice.FindFdtiUsbDevices().Where(p => p.nodeDescription == "FT245R USB FIFO").Count() == 0)
            {
                Thread.Sleep(100);
            }

            var testPacket = new CommandPacket(CommandPacket.Command.TestConnection);
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

                    if (receiveBuffer[3] == (char)'k' && receiveBuffer[4] == (char)'3') //(Unofficial OS EDV3)
                    {
                        Console.WriteLine("Connected to EverDrive64 V3 on port: {0}", device.nodeComportName);
                        switch ((char)receiveBuffer[5])
                        {
                            case 'p':
                                Console.WriteLine("PAL region detected");
                                break;
                            case 'n':
                                Console.WriteLine("NTSC region detected");
                                break;
                            case 'm':
                                Console.WriteLine("MPAL region detected");
                                break;
                            default:
                                Console.WriteLine("Unknown region detected");
                                break;
                        }
                        return true;
                    }
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
                if ((long)(fileLength / CHUNK_64KB * CHUNK_64KB) != fileStream.Length) //65536
                {
                    fileLength = (int)(fileStream.Length / CHUNK_64KB * CHUNK_64KB + CHUNK_64KB);
                }
                fileArray = new byte[fileLength];
                fileStream.Read(fileArray, 0, (int)fileStream.Length);
            }
            Console.WriteLine("File Size: " + fileLength);

            if (fileLength < CHUNK_2MB) //needs filling
            {
                Console.WriteLine("Preparing space (zero fill) for ROM on flash cart");
                var fillPacket = new CommandPacket(CommandPacket.Command.Fill);
                fillPacket.Send(IoPort);


                IoPort.Read(receiveBuffer, 0, 512);

                if (receiveBuffer[3] != 107)
                {
                    Console.WriteLine("Zero fill failed, exiting");
                    fileArray = null;
                    return;
                }
                else
                {
                    Console.WriteLine("Zero fill succeeded");
                }
            }

            var writePacket = new CommandPacket(CommandPacket.Command.WriteRom);

            var commandInfo = new byte[4];
            commandInfo[0] = 0; //offset
            commandInfo[1] = 0; //offset
            commandInfo[2] = (byte)(fileArray.Length / 512 >> 8);
            commandInfo[3] = (byte)(fileArray.Length / 512);

            writePacket.body(commandInfo);
            writePacket.Send(IoPort);

            Console.WriteLine("Sending ROM to flash cart...");
            DateTime now = DateTime.Now;
            for (int l = 0; l < fileArray.Length; l += CHUNK_32KB)
            {
                if (l == CHUNK_32MB)
                {
                    Console.WriteLine("Sending next 32MB chunk");

                    commandInfo[0] = 0x40; //64 (offset 32MB)
                    commandInfo[1] = 0;
                    commandInfo[2] = (byte)((fileArray.Length - CHUNK_32MB) / 512 >> 8);
                    commandInfo[3] = (byte)((fileArray.Length - CHUNK_32MB) / 512);
                    writePacket.body(commandInfo);
                    writePacket.Send(IoPort);
                }
                //TODO: why does the test code work but the real code not?
                //TEST code
                byte[] test = new byte[CHUNK_32KB];
                Array.Copy(fileArray, l, test, 0, CHUNK_32KB);
                IoPort.Write(test, 0, test.Length);
                // TEST code
                //IoPort.Write(fileArray, l, CHUNK_32KB);

                if (l % CHUNK_512KB == 0)
                {
                    Console.WriteLine("Sent 512KB chunk: {0} to {1} of {2}", l, l + CHUNK_512KB, fileArray.Length);
                }
            }
            Console.WriteLine("File sent.");
            Console.WriteLine("Elapsed time: {0}ms", (DateTime.Now - now).TotalMilliseconds);
            fileArray = null;
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
