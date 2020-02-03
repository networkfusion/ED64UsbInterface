using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N64LoaderConsole
{
    public static class Screenshot
    {
        public static void TakeScreenshot(SerialPortStream IoPort, string filename)
        {
            //TODO: Is there a better way to receive the current resolution from the ED64!
            var resPacket = new CommandPacket(CommandPacket.Command.ScreenResolution);
            resPacket.Send(IoPort);

            var resReceiveBuffer = new byte[512];

            IoPort.Read(resReceiveBuffer, 0, 512);

            var width = BitConverter.ToUInt16(resReceiveBuffer, 0);
            var height = BitConverter.ToUInt16(resReceiveBuffer, 2);



            var picturePacket = new CommandPacket(CommandPacket.Command.Picture);
            picturePacket.Send(IoPort);


            var dataSize = width * height * 2; //Colour Data(2 bytes)
            var imageSize = width * height * 3; //Colour Data(3 bytes)
            var receiveBuffer = new List<byte>();

            int bytesRead = 0;

            int i = 0;
            while (i < (dataSize))
            {
                //I think we should be using a memory stream?
                //currently doing it one by one, otherwise we miss stuff!
                var tempBuffer = new byte[1];
                var tempRead = IoPort.Read(tempBuffer, 0, 1);
                if (tempRead == 1)
                {
                    receiveBuffer.AddRange(tempBuffer);
                }
                bytesRead += tempRead;
                i++;
            }

            Console.WriteLine($" Read Bytes: {bytesRead}");

            var bmpHeader = new byte[54] { 
                0x42, 0x4D, //(BM)
                0x36, 0x10, 0x0E, 0x00, //(File size)
                0x00, 0x00, //(reserved)
                0x00, 0x00, //(reserved)
                0x36, 0x00, 0x00, 0x00, //(offset = header size + info header size 54 bytes)
                //BITMAPINFOHEADER
                0x28, 0x00, 0x00, 0x00, //(Length = 40 bytes)
                0x80, 0x02, 0x00, 0x00, //(Bitmap width signed int = 640)
                0x20, 0xFE, 0xFF, 0xFF, //(Bitmap height signed int = -480 for "topdown")
                0x01, 0x00, //(number of colour planes MUST BE 1)
                0x18, 0x00, //(number of bits per pixel = 24)
                0x00, 0x00, 0x00, 0x00, //(compression)
                0x00, 0x10, 0x0E, 0x00, //(image size)
                0x00, 0x00, 0x00, 0x00, //(horizontal res) 3790
                0x00, 0x00, 0x00, 0x00, //(vertical res) 3800
                0x00, 0x00, 0x00, 0x00, //(number of colours)
                0x00, 0x00, 0x00, 0x00 //(number of important colours)
            };

            //filesize = imagesize + 54 //generally it is not used, but we will set it just incase!
            var filesize = imageSize + 54;
            bmpHeader[2] = (byte)(filesize & 0xff);
            bmpHeader[3] = (byte)(filesize >> 8);
            bmpHeader[4] = 0;
            bmpHeader[5] = 0;

            //image width (using short as the max res is 640)
            bmpHeader[18] = (byte)(width & 0xff);
            bmpHeader[19] = (byte)(width >> 8);
            bmpHeader[20] = 0;
            bmpHeader[21] = 0;

            //negitive height for "top-down" bitmap (using short as the max res is 480)
            var topdownHeight = height * -1;
            bmpHeader[22] = (byte)(topdownHeight & 0xff);
            bmpHeader[23] = (byte)(topdownHeight >> 8);
            bmpHeader[24] = 0xff;
            bmpHeader[25] = 0xff;

            //imagesize //generally it is not used, but we will set it just incase!
            bmpHeader[34] = (byte)(imageSize & 0xff);
            bmpHeader[35] = (byte)(imageSize >> 8);
            bmpHeader[36] = 0;
            bmpHeader[37] = 0;

            var imageData = new List<byte>();
            imageData.AddRange(bmpHeader);

            using (BinaryReader stream = new BinaryReader(new MemoryStream(receiveBuffer.ToArray())))
            {

                for (int h = 0; h < height; h++)
                {
                    for (int w = 0; w < width; w++)
                    {
                        var colour = stream.ReadBytes(2);

                        int red = (colour[0] & 0xf8);
                        int green = ((colour[0] & 0x07) << 5) | ((colour[1] & 0xc0) >> 3);
                        int blue = (colour[1] & 0x3e) << 2;

                        imageData.Add((byte)blue);
                        imageData.Add((byte)green);
                        imageData.Add((byte)red);
                    }
                }
            }

            using (var fs = new FileStream(filename, FileMode.OpenOrCreate))
            {
                fs.Write(imageData.ToArray(), 0, imageData.Count);
            }
            Console.WriteLine("Screenshot taken!");
        }  

    }
}
