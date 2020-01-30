using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


//based on https://raw.githubusercontent.com/masl123/n64RomConverter/master/com/github/masl123/n64RomConverter/Program.java
// good resource http://jul.rustedlogic.net/thread.php?id=11769

namespace RomConverter
{
    public static class RomConverter
    {
        public enum RomType : uint
        {
            z64 = 0x40123780, //1074935680
            n64 = 0x37804012, //931151890
            v64 = 0x80371240 //2151092800
        }

        public static string ConvertFile(string InFilePath, RomType OutFileType)
        {
           return ConvertFile(InFilePath, new FileInfo(InFilePath).DirectoryName, OutFileType);
        }

        public static string ConvertFile(string InFilePath, string OutDirPath, RomType OutFileType)
        {
            Debug.WriteLine("Converting n64 Roms from [n64,v64,z64] to [n64,v64,z64]");
            try {
                var OutFileName = OutDirPath + "\\" + Path.GetFileNameWithoutExtension(new FileInfo(InFilePath).ToString()) + "." + OutFileType.ToString();

                if (InFilePath == OutFileName)
                {
                    return InFilePath;
                }

                //Just be sure we don't try to read a huge file into memory. (Max File size is 128MB)
                if (new FileInfo(InFilePath).Length > 134217728)
                {
                    throw new Exception("File too large");
                }
                else
                {

                    using (FileStream infs = File.OpenRead(InFilePath))
                    {
                        

                        if (File.Exists(OutFileName))
                        {
                            Debug.WriteLine("Overwriting File " + OutFileName);
                        }

                        using (FileStream outfs = File.Create(OutFileName))
                        {
                            //Read input File;
                            byte[] inBuffer = new byte[(int)infs.Length];
                            infs.Read(inBuffer, 0, (int)infs.Length);

                            //get file header type
                            infs.Seek(0, SeekOrigin.Begin);

                            RomType romHeader = 0;
                            byte[] headerArray = new byte[4];
                            infs.Read(headerArray, 0, 4);
                            if (BitConverter.IsLittleEndian)
                            {
                                Array.Reverse(headerArray);
                            }
                            romHeader = (RomType)Enum.ToObject(typeof(RomType), BitConverter.ToUInt32(headerArray, 0));

                            infs.Seek(0, SeekOrigin.Begin);

                            //get the right format of the original ROM
                            switch (romHeader)
                            {

                                case RomType.n64:
                                    switch (OutFileType)
                                    {
                                        case RomType.n64:
                                            //nothing to do here
                                            break;
                                        case RomType.v64:
                                            wordSwap(dWordSwap(inBuffer));
                                            break;
                                        case RomType.z64:
                                            dWordSwap(inBuffer);
                                            break;
                                        default:
                                            break;
                                    }
                                    break;

                                case RomType.z64:
                                    switch (OutFileType)
                                    {
                                        case RomType.n64:
                                            dWordSwap(inBuffer);
                                            break;
                                        case RomType.v64:
                                            wordSwap(inBuffer);
                                            break;
                                        case RomType.z64:
                                            //nothing to do here
                                            break;
                                        default:
                                            break;
                                    }
                                    break;

                                case RomType.v64:
                                    switch (OutFileType)
                                    {
                                        case RomType.n64:
                                            dWordSwap(wordSwap(inBuffer));
                                            break;
                                        case RomType.v64:
                                            //nothing to do here
                                            break;
                                        case RomType.z64:
                                            wordSwap(inBuffer);
                                            break;
                                        default:
                                            break;
                                    }
                                    break;

                                default:
                                    throw new Exception("Unrecognised File");
                            }


                            outfs.Write(inBuffer, 0, inBuffer.Length);

                        }
                    }
                }

                Debug.WriteLine("finished conversion");
                return OutFileName;
            } catch (Exception e) {
                Debug.WriteLine(e);
            }
            return "";
        }



        internal static byte[] wordSwap(byte[] inFile)
        {
            for (int i = 0; i < inFile.Length; i += 2) {
                wordSwap(inFile, i, i + 1);
            }
            return inFile;
        }


        internal static byte[] wordSwap(byte[] inFile, int a, int b)
        {
            byte temp = inFile[a];
            inFile[a] = inFile[b];
            inFile[b] = temp;
            return inFile;
        }



        internal static byte[] dWordSwap(byte[] inFile)
        {
            for (int i = 0; i < inFile.Length; i += 4) {
                wordSwap(inFile, i, i + 3);
                wordSwap(inFile, i + 1, i + 2);
            }
            return inFile;
        }


        //We should try the following methods for faster processing:
        public static ushort SwapBytes(ushort x)
        {
            return (ushort)((ushort)((x & 0xff) << 8) | ((x >> 8) & 0xff));
        }

        public static uint SwapBytes(uint x)
        {
            return ((x & 0x000000ff) << 24) +
                   ((x & 0x0000ff00) << 8) +
                   ((x & 0x00ff0000) >> 8) +
                   ((x & 0xff000000) >> 24);
        }


        // reverse byte order (16-bit)
        public static ushort ReverseBytes(ushort value)
        {
            return (ushort)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        // reverse byte order (32-bit)
        public static uint ReverseBytes(uint value)
        {
            return (value & 0x000000FFU) << 24 
                | (value & 0x0000FF00U) << 8 
                | (value & 0x00FF0000U) >> 8 
                | (value & 0xFF000000U) >> 24;
        }


    }
}
