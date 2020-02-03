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
            var picturePacket = new CommandPacket(CommandPacket.Command.Picture);
            picturePacket.Send(IoPort);

            //TODO: Should really pole the ED64 for its current resolution and go from there!
            var width = 640;
            var height = 480;

            var dataSize = width * height * 2; //Colour Data(2 bytes)
            //var imageSize = width * height * 3; //Colour Data(3 bytes) //921600
            var receiveBuffer = new List<byte>();//[dataSize]; //614400 (1200 * 512) 0x96000 

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
                //Console.WriteLine($"received {tempRead} chunk");
                i++;
            }

            Console.WriteLine($" Read Bytes: {bytesRead}");

            var header = new byte[54] { 
                0x42, 0x4D, //(BM)
                0x36, 0x10, 0x0E, 0x00, //(File size)
                0x00, 0x00, //(reserved)
                0x00, 0x00, //(reserved)
                0x36, 0x00, 0x00, 0x00, //(offset = header size + info header size 54 bytes)
                //BITMAPINFOHEADER
                0x28, 0x00, 0x00, 0x00, //(Length = 40 bytes)
                0x80, 0x02, 0x00, 0x00, //(Bitmap width signed int = 640)
                //0xE0, 0x01, 0x00, 0x00, //(Bitmap height signed int = 480)
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

            //width = 280
            //bmphead[0x12] = width & 0xff
            //bmphead[0x13] = width >> 8
            //bmphead[0x14] = 0
            //bmphead[0x15] = 0

            //# -256 for "top-down" bitmap
            //height = 256;
            //header[0x16] = 0x00;
            //header[0x17] = 0xFF;
            //header[0x18] = 0xFF;
            //header[0x19] = 0xFF;

            var imageData = new List<byte>();
            imageData.AddRange(header);

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
