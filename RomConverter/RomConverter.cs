using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


//based on https://raw.githubusercontent.com/masl123/n64RomConverter/master/com/github/masl123/n64RomConverter/Program.java

namespace RomConverter
{
    public class RomConverter
    {
        public enum OutputType
        {
            n64,
            v64,
            z64
        }

        public async Task ConvertFile(string InFilePath, OutputType OutFileType)
        {
            await ConvertFile(InFilePath, new FileInfo(InFilePath).DirectoryName, OutFileType);
        }

        public async Task ConvertFile(string InFilePath, string OutDirPath, OutputType OutFileType)
        {
            Debug.WriteLine("Converting n64 Roms from [n64,v64,z64] to [n64,v64,z64]");
            try {
                //Just be sure we don't try to read a huge file into memory. (Max File size is 128MB)
                if (new FileInfo(InFilePath).Length > 134217728)
                {
                    throw new Exception("File too large");
                }
                else
                {
                    using (FileStream infs = File.OpenRead(InFilePath))
                    {
                        var OutFileName = OutDirPath + new FileInfo(InFilePath).Name + OutFileType.ToString();

                        if (File.Exists(OutFileName))
                        {
                            Debug.WriteLine("Overwriting File" + OutFileName);
                        }

                        using (FileStream outfs = File.Create(OutFileName))
                        {
                            //Read input File;
                            byte[] inBuffer = new byte[(int)infs.Length];
                            await infs.ReadAsync(inBuffer, 0, (int)infs.Length);

                            //get file header type
                            infs.Seek(0, SeekOrigin.Begin);
                            byte[] headerArray = new byte[4];
                            infs.(headerArray, 0, 4);
                            uint RomHeader = BitConverter.ToUInt32(headerArray, 0);

                            infs.Seek(0, SeekOrigin.Begin);

                            //get the right format of the original ROM
                            switch (RomHeader)
                            {

                                case 0x40123780:
                                    //n64
                                    switch (OutFileType)
                                    {
                                        case OutputType.n64:
                                            //nothing to do here
                                            break;
                                        case OutputType.v64:
                                            wordSwap(dWordSwap(inBuffer));
                                            break;
                                        case OutputType.z64:
                                            dWordSwap(inBuffer);
                                            break;
                                        default:
                                            break;
                                    }
                                    break;

                                case 0x80371240:
                                    //z64
                                    switch (OutFileType)
                                    {
                                        case OutputType.n64:
                                            dWordSwap(inBuffer);
                                            break;
                                        case OutputType.v64:
                                            wordSwap(inBuffer);
                                            break;
                                        case OutputType.z64:
                                            //nothing to do here
                                            break;
                                        default:
                                            break;
                                    }
                                    break;

                                case 0x37804012:
                                    //v64
                                    switch (OutFileType)
                                    {
                                        case OutputType.n64:
                                            dWordSwap(wordSwap(inBuffer));
                                            break;
                                        case OutputType.v64:
                                            //nothing to do here
                                            break;
                                        case OutputType.z64:
                                            wordSwap(inBuffer);
                                            break;
                                        default:
                                            break;
                                    }
                                    break;

                                default:
                                    throw new Exception("Unrecognised File");
                            }


                            await outfs.WriteAsync(inBuffer, 0, inBuffer.Length);

                        }
                    }
                }

                Debug.WriteLine("finished conversion");
            } catch (Exception e) {
                Debug.WriteLine(e);
            }
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
        public ushort SwapBytes(ushort x)
        {
            return (ushort)((ushort)((x & 0xff) << 8) | ((x >> 8) & 0xff));
        }

        public uint SwapBytes(uint x)
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
