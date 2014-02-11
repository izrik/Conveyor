using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace Conveyor.NDeproxy
{
    public static class SocketHelper
    {
        static readonly Logger log = new Logger("SocketHelper");

        public static Socket Server(int port, int listenQueue = 5)
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.Bind(new IPEndPoint(IPAddress.Any, port));
            s.Listen(listenQueue);
            return s;
        }

        public static Socket Client(string remoteHost, int port, int timeout = Timeout.Infinite)
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (timeout == Timeout.Infinite)
            {
                s.Connect(remoteHost, port);
                return s;
            }
            else
            {
                var result = s.BeginConnect(remoteHost, port, s.EndConnect, s);

                if (result.AsyncWaitHandle.WaitOne(timeout))
                {
                    return s;
                }
                else
                {
                    throw new SocketException((int)SocketError.TimedOut);
                }
            }
        }

        public static int GetLocalPort(this Socket socket)
        {
            return ((IPEndPoint)socket.LocalEndPoint).Port;
        }

        public static int GetRemotePort(this Socket socket)
        {
            return ((IPEndPoint)socket.RemoteEndPoint).Port;
        }

        public static bool IsClosed(this Socket socket, int timeoutMicroSeconds = 1)
        {
            return (socket.Poll(timeoutMicroSeconds, SelectMode.SelectRead) && socket.Available < 1);
        }


    }
}

