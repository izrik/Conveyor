using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Conveyor
{
    public class LiterateSocket : TextReader
    {
        public readonly UnbufferedSocketStream Stream;
        public readonly StreamWriter Writer;

        public LiterateSocket(Socket socket)
        {
            Stream = new UnbufferedSocketStream(socket);
            Writer = new StreamWriter(Stream);
            Writer.NewLine = "\r\n";
        }

        public override int Read()
        {
            int value = Stream.ReadByte();

            if (value < 0)
                return -1;

            return (byte)value;
        }

        public override string ReadLine()
        {
            int value = this.Read();
            if (value < 0)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();

            // naive definition of line termination
            // just consider \n, not \r or \r\n
            while (value >= 0 && value != '\n')
            {
                char ch = (char)value;
                if (ch != '\r')
                {
                    sb.Append(ch);
                }
                value = this.Read();
            }

            return sb.ToString();
        }

        public class UnbufferedSocketStream : Stream
        {
            readonly Socket _socket;

            public UnbufferedSocketStream(Socket socket)
            {
                if (socket == null)
                    throw new ArgumentNullException("socket");

                _socket = socket;
            }

            #region implemented abstract members of Stream

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _socket.Receive(buffer, offset, count, SocketFlags.None);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _socket.Send(buffer, offset, count, SocketFlags.None);
            }

            public override bool CanRead { get { return true; } }

            public override bool CanSeek { get { return false; } }

            public override bool CanWrite { get { return true; } }

            public override long Length
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public override long Position
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            #endregion

        }
    }
}
