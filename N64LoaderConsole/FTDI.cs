using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FTD2XX_NET;

namespace N64LoaderConsole
{
    internal static class FindDevice
    {

        public static List<FDTIPort> FindFdtiUsbDevices()
        {
            ///////////////////////
            // Requires 
            // FTD2XX_NET.dll
            ///////////////////////

            List<FDTIPort> ports = new List<FDTIPort>();

            FTDI _ftdi = new FTDI();
            

            UInt32 count = 0;
            FTDI.FT_STATUS status = _ftdi.GetNumberOfDevices(ref count);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                Console.WriteLine("log.Warn: Unable to access FTDI");
                return ports;
            }

            FTDI.FT_DEVICE_INFO_NODE[] list = new FTDI.FT_DEVICE_INFO_NODE[count];
            status = _ftdi.GetDeviceList(list);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                Console.WriteLine("log.Warn: Unable to access FTDI");
                return ports;
            }


            foreach (FTDI.FT_DEVICE_INFO_NODE node in list)
            {
                if ((status = _ftdi.OpenByLocation(node.LocId)) == FTDI.FT_STATUS.FT_OK)
                {
                    try
                    {
                        string comport;
                        _ftdi.GetCOMPort(out comport);

                        if (comport != null && comport.Length > 0)
                        {
                            //Console.WriteLine(node.Type);
                            ports.Add(new FDTIPort(comport, node.Description.ToString(), node.SerialNumber.ToString()));
                        }
                    }
                    finally
                    {
                        _ftdi.Close();
                    }
                }
            }

            //_ftdi.Dispose();
            return ports;
        }

        public class FDTIPort
        {
            private string _nodeComportName = "";
            private string _nodeDescription = "";
            private string _nodeSerialNumber = "";

            // Constructor
            public FDTIPort()
            {
                _nodeComportName = "";
                _nodeDescription = "";
                _nodeSerialNumber = "";
            }
            // Constructor

            public FDTIPort(string nodeComportName, string nodeDescription, string nodeSerialNumber)
            {
                _nodeComportName = nodeComportName;
                _nodeDescription = nodeDescription;
                _nodeSerialNumber = nodeSerialNumber;
            }

            public string nodeComportName
            {
                get { return this._nodeComportName; }
            }

            public string nodeDescription
            {
                get { return this._nodeDescription; }
            }

            public string nodeSerialNumber
            {
                get { return this._nodeSerialNumber; }
            }

        }
    }
}
