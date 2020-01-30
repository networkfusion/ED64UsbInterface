using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Interop;
using USBClassLibrary;

namespace N64LoaderConsole
{
    public class EverDriveUSB
    {
        public Connection Connection { get; set; }
        public EverDriveUSB()
        {
            Connection = new Connection();

            //Throttle = new Throttle(Connection.IoPort);
            //LocoProgrammer = new LocoProgrammer(Connection.IoPort);

            //USB Connection
            _usbPort = new UsbClass();
            _listOfUsbList = new List<UsbClass.DeviceProperties>();
            _usbPort.UsbDeviceAttached += USBPort_USBDeviceAttached;
            _usbPort.UsbDeviceRemoved += USBPort_USBDeviceRemoved;



            //USB Connection (A class library doesnt have a window, so we will pass a fake one!)
            var source = new HwndSource(0, 0, 0, 0, 0, "fakeWindow", IntPtr.Zero);
            source.AddHook(WndProc);
            _usbPort.RegisterForDeviceChange(true, source.Handle);
            UsbTryMyDeviceConnection();
            _myUsbDeviceConnected = false;

        }

        private readonly UsbClass _usbPort;
        private List<UsbClass.DeviceProperties> _listOfUsbList;

        private bool _myUsbDeviceConnected;

        private const uint MyDeviceVid = 0X0403; //FTDI 245R VID
        private const uint MyDevicePid = 0X6001; //FTDI 245R PID

        #region USB
        /// <summary>
        /// Try to connect to the device.
        /// </summary>
        /// <returns>True if success, false otherwise</returns>
        private bool UsbTryMyDeviceConnection()
        {
            uint? mi = null;
            const bool bGetSerialPort = true;

            if (UsbClass.GetUsbDevice(MyDeviceVid, MyDevicePid, ref _listOfUsbList, bGetSerialPort, mi))
            {
                Connect();
                return true;
            }
            else
            {
                Disconnect();
                return false;
            }
        }

        private void USBPort_USBDeviceAttached(object sender, UsbClass.UsbDeviceEventArgs e)
        {
            if (!_myUsbDeviceConnected)
            {
                if (UsbTryMyDeviceConnection())
                {
                    _myUsbDeviceConnected = true;
                }
            }
        }

        private void USBPort_USBDeviceRemoved(object sender, UsbClass.UsbDeviceEventArgs e)
        {
            if (!UsbClass.GetUsbDevice(MyDeviceVid, MyDevicePid, ref _listOfUsbList, false))
            {
                //My Device is removed
                _myUsbDeviceConnected = false;
                Disconnect();
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            _usbPort.ProcessWindowsMessage(msg, wParam, lParam, ref handled);

            return IntPtr.Zero;
        }

        private async void Connect()
        {
            var retries = 0;
            do
            {
                try
                { //this causes an exception for user controls. we need to make them use bindings!
                    Connection.Port = _listOfUsbList[0].ComPort;
                    await Connection.Connect();
                    Console.Out.WriteLine("Connected to elink");
                }
                catch (Exception ex)
                {
                    retries++;
                    Console.Out.WriteLine(ex.Message.ToString());
                }
            }
            while (Connection.State != ConnectionState.Connected && retries < 4);
        }

        private async void Disconnect()
        {
            //TO DO: Insert your disconnection code here
            await Connection.Disconnect();
            Console.Out.WriteLine("Disconnected from ED64");
        }
        #endregion

    }
}
