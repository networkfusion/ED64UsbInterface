using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace N64LoaderConsole
{
    public enum ConnectionState
    {
        Connecting,
        Connected,
        Disconnecting,
        Disconnected,
        Unknown,
        Error,
    }

    public class Connection : INotifyPropertyChanged
    {
        public SerialPortStream IoPort = new SerialPortStream();
        public string Port { get { return IoPort.PortName; } set { IoPort.PortName = value; } }

        private ConnectionState _state = ConnectionState.Disconnected;
        public ConnectionState State
        {
            get { return _state; }
            internal set
            {
                _state = value;
                OnPropertyChanged();
            }


        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }


        public Connection()
        {
        }

        public Connection(string port)
            : this()
        {
            Port = port;
        }

        void InitialiseConnection()
        {
            //write timeout is needed if writing async as usbser doesnt clear the buffer correctly.
            IoPort.WriteTimeout = 100;
            //IoPort.DataReceived += serialPort_DataReceived;
        }

        private bool _readInProgress = false; //TODO: this maybe better as a lock object

        //private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        //{
        //    Task.Run(async () =>
        //    {
        //        if (!_readInProgress)
        //        {
        //            _readInProgress = true;
        //            do
        //            {
        //                await PacketHandler.ReadPacketAsync(IoPort);
        //            } while (IoPort.BytesToRead > 0);
        //            //this is necessary as some packets are sent so close together that the dataReceived event is not fired.

        //            _readInProgress = false;
        //        }
        //    });
        //}

        public async Task Connect()
        {
            IoPort.PortName = Port;
            InitialiseConnection();

            if (IoPort.IsOpen)
            {
                await Disconnect();
            }

            State = ConnectionState.Connecting;
            Console.Out.WriteLine("Connecting using Port: {0}", IoPort.PortName);

            await Task.Run(() => IoPort.OpenDirect());

            var msg = new CommandPacket(CommandPacket.Command.TestConnection);
            char resp;
            var initialisationDateTime = DateTime.Now;
            var timeout = false;
            await msg.SendAsync(IoPort);
            do
            {
                resp = (char)IoPort.ReadByte();
                if (DateTime.Now > initialisationDateTime.AddMilliseconds(1000))
                    timeout = true;

            } while (resp != 'k' && timeout == false);

            if (timeout == true)
            {
                State = ConnectionState.Error;
                throw new Exception("Connection didn't initiate properly (failed to get software version)");
            }

            State = ConnectionState.Connected;
        }


        private static async Task PerformUpdate()
        {
            //TODO: Perform Update Here!
            await Task.Run(() => Console.Out.WriteLine("Update Required Placeholder!"));
        }




        public async Task Disconnect()
        {
            await Task.Run(() =>
            {
                if (IoPort.IsOpen)
                {
                    State = ConnectionState.Disconnecting;
                    try
                    {
                        //IoPort.DataReceived -= serialPort_DataReceived;
                        IoPort.DiscardInBuffer();
                        IoPort.DiscardOutBuffer();
                        IoPort.Close();
                        Console.Out.WriteLine("Closed serialport.");
                    }
                    catch (Exception)
                    {
                        Console.Out.WriteLine("Failed to close serialport.");
                    }


                    State = ConnectionState.Disconnected;
                }
            });
        }

        ~Connection()
        {
            //IoPort.DataReceived -= serialPort_DataReceived;
            IoPort.Dispose();
        }

    }
}
