using System;
using Conveyor.NDeproxy;
using System.Threading;
using System.Net.Sockets;

namespace Conveyor
{
    public class ListenerThread
    {
        static readonly Logger log = new Logger("ListenerThread");
        public Server Parent;
        public Socket Socket;
        public Thread Thread;
        readonly ManualResetEvent _stopSignal = new ManualResetEvent(false);
        readonly RequestHandler _requestHandler;

        public ListenerThread(Server parent, Socket socket, string name, RequestHandler requestHandler)
        {
            this.Parent = parent;
            parent.AddShutdownAction(this.Stop);
            this.Socket = socket;
            _requestHandler = requestHandler;

            Thread = new Thread(this.Run);
            Thread.Name = name;
            Thread.IsBackground = true;
            Thread.Start();
        }

        public void Run()
        {
            var acceptedSignal = new ManualResetEvent(false);
            var acceptedOrStop = new WaitHandle[2] { acceptedSignal, _stopSignal };

            try
            {
                while (!_stopSignal.WaitOne(0))
                {
                    Socket handlerSocket = null;

                    try
                    {
                        log.Debug("calling BeginAccept");
                        acceptedSignal.Reset();
                        var ar = Socket.BeginAccept((_ar) =>
                            {
                                log.Debug("calling EndAccept");
                                try
                                {
                                    handlerSocket = Socket.EndAccept(_ar);
                                }
                                catch (Exception ex)
                                {
                                    log.Debug("Caught an exception in EndAccept: {0}", ex);
                                }
                                log.Debug("returned from EndAccept, signalling completion");
                                acceptedSignal.Set();
                            }, null);
                        log.Debug("returned from BeginAccept");

                        log.Debug("waiting on the wait handles");
                        var signal = WaitHandle.WaitAny(acceptedOrStop);
                        if (acceptedOrStop[signal] == _stopSignal)
                        {
                            break;
                        }

                        log.Debug("Accepted a new connection");

                        log.Debug("Starting the handler thread");
                        HandlerThread handlerThread = new HandlerThread(Parent, handlerSocket, Thread.Name + string.Format("-connection-{0}", Environment.TickCount), _requestHandler);
                        handlerSocket = null;
                        log.Debug("Handler thread started");

                    }
                    catch (Exception e)
                    {
                        log.Error("Exception caught in Run, in the second try-block: {0}", e);
                        throw;
                    }
                    finally
                    {
                        if (handlerSocket != null)
                        {
                            handlerSocket.Close();
                        }
                    }
                }
            }
            finally
            {
                Socket.Close();
            }
        }

        public void Stop()
        {
            _stopSignal.Set();

            if (Thread.IsAlive)
            {
                int time = Environment.TickCount;
                Thread.Join(1000);
                log.Debug("while Stop()ing, joined for {0} ms", Environment.TickCount - time);
            }
            if (Thread.IsAlive)
            {
                Thread.Abort();
            }
        }
    }

}

