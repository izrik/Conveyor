using System;
using System.Net.Sockets;
using System.Threading;
using Conveyor.NDeproxy;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;

namespace Conveyor
{
    public class Server : IDisposable
    {
        static readonly Logger log = new Logger("Conveyor");
        public readonly ListenerThread ListenerThread;
        readonly Socket serverSocket;
        public readonly int Port;

        readonly List<Action> _shutdownActions = new List<Action>();
        readonly object _shutdownActionsLock = new object();

        public Server(int port, RequestHandler requestHandler)
        {
            this.Port = port;

            serverSocket = SocketHelper.Server(port);

            ListenerThread = new ListenerThread(this, serverSocket, string.Format("Listener-{0}", port), requestHandler);
        }

        public static readonly string VERSION = getVersion();
        public static readonly string VERSION_STRING = string.Format("Conveyor {0}", VERSION);

        private static string getVersion()
        {
            var assembly = Assembly.GetCallingAssembly();
            var name = assembly.GetName();
            var version = name.Version;
            return version.ToString();
        }

        void IDisposable.Dispose()
        {
            shutdown();
        }
        public void shutdown()
        {
            ExecuteShutdownActions();

            if (ListenerThread != null)
            {
            }
            if (serverSocket != null)
            {
                serverSocket.Close();
            }
        }

        internal void AddShutdownAction(Action action)
        {
            lock (_shutdownActionsLock)
            {
                _shutdownActions.Add(action);
            }
        }

        internal void ExecuteShutdownActions()
        {
            lock (_shutdownActionsLock)
            {
                foreach (var action in _shutdownActions)
                {
                    action();
                }

                _shutdownActions.Clear();
            }
        }
    }
}

