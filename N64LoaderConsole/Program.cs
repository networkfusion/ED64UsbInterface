using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
//using System.IO.Ports;
using RJCP.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace N64LoaderConsole
{
    internal class Program
    {
        //private static SerialPort port;
        private static SerialPortStream port = new SerialPortStream();
        private static bool connected = false;
        private static byte[] sendBuffer = new byte[512];
        private static byte[] receiveBuffer = new byte[512];

        private static void Main(string[] args)
        {


            Console.WriteLine("**********************************");
            Console.WriteLine("ED64 usb loader v1.0");

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
                Console.WriteLine("ED64 usb port not detected");
                return;
            }

            WriteRom(args[0]);

            
            var startPacket = new CommandPacket(CommandPacket.Command.StartRom);
            //sendBuffer[3] = 83; //S (pif boot)
            //port.Write(sendBuffer, 0, 512);
            startPacket.Send(port);
            port.Close();
        }

        private static void InitialiseSerialPort()
        {
            //string[] portNames = port.GetPortNames();
            string[] portNames = SerialPortStream.GetPortNames();
            
            //sendBuffer[0] = 67; //C
            //sendBuffer[1] = 77; //M
            //sendBuffer[2] = 68; //D
            //sendBuffer[3] = 84; //T (test)
            var testPacket = new CommandPacket(CommandPacket.Command.TestConnection);

            for (int i = 0; i < portNames.Length; i++)
            {
                try
                {
                    //port = new SerialPort(portNames[i]);
                    port = new SerialPortStream(portNames[i]);
                    port.WriteTimeout = 10500;
                    port.ReadTimeout = 10500;
                    port.Open();
                    //port.Write(sendBuffer, 0, 512);
                    port.Write(testPacket.Packet, 0, testPacket.Packet.Length);
                    //testPacket.Send(port);
                    Thread.Sleep(100);

                    port.Read(receiveBuffer, 0 , 4);
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
            FileStream fileStream = File.OpenRead(fileName);
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
                //sendBuffer[0] = 67; //C
                //sendBuffer[1] = 77; //M
                //sendBuffer[2] = 68; //D
                //sendBuffer[3] = 70; //F (fill)
                var fillPacket = new CommandPacket(CommandPacket.Command.Fill);

                //port.Write(sendBuffer, 0, 512);
                fillPacket.Send(port);
                for (int k = 0; k < 512; k++)
                {
                    port.Read(sendBuffer, k, 512 - k); //surely this should be the receive buffer!
                }
                if (sendBuffer[3] != 107) //surely this should be the receive buffer!
                {
                    Console.WriteLine("fill error");
                    port.Close();
                    return;
                }
                Console.WriteLine("fill ok");
            }
            //sendBuffer[0] = 67; //C
            //sendBuffer[1] = 77; //M
            //sendBuffer[2] = 68; //D
            //sendBuffer[3] = 87; //W (write)
            var writePacket = new CommandPacket(CommandPacket.Command.WriteRom, 4);
            //sendBuffer[4] = 0; //offset
            //sendBuffer[5] = 0; //offset
            //sendBuffer[6] = (byte)(fileArray.Length / 512 >> 8);
            //sendBuffer[7] = (byte)(fileArray.Length / 512);
            var commandInfo = new byte[4];
            commandInfo[0] = 0; //offset
            commandInfo[1] = 0; //offset
            commandInfo[2] = (byte)(fileArray.Length / 512 >> 8);
            commandInfo[3] = (byte)(fileArray.Length / 512);
            writePacket.body(commandInfo);

            //port.Write(sendBuffer, 0, 512);
            writePacket.Send(port);

            Console.WriteLine("sending...");
            DateTime now = DateTime.Now;
            for (int l = 0; l < fileArray.Length; l += 32768)
            {
                if (l == 33554432)
                {
                    Console.WriteLine("next 32m!!!!!!!!!!!!!!!");
                    //sendBuffer[4] = 64; //0x40 - 32mb offset
                    //sendBuffer[5] = 0;
                    //sendBuffer[6] = (byte)((fileArray.Length - 33554432) / 512 >> 8);
                    //sendBuffer[7] = (byte)((fileArray.Length - 33554432) / 512);
                    commandInfo[0] = 64; //0x40 - 32mb offset
                    commandInfo[1] = 0;
                    commandInfo[2] = (byte)((fileArray.Length - 33554432) / 512 >> 8);
                    commandInfo[3] = (byte)((fileArray.Length - 33554432) / 512);
                    writePacket.body(commandInfo);

                    //port.Write(sendBuffer, 0, 512);
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
