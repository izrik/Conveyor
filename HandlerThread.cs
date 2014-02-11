using System;
using Conveyor.NDeproxy;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Conveyor
{
    public class HandlerThread
    {
        static readonly Logger log = new Logger("HandlerThread");
        public readonly Server Parent;
        public readonly Thread Thread;
        public Socket Socket { get { return _socket; } }
        Socket _socket;
        readonly ManualResetEvent _stopSignal = new ManualResetEvent(false);
        readonly RequestHandler _requestHandler;

        public HandlerThread(Server parent, Socket socket, string threadName, RequestHandler requestHandler)
        {
            if (parent == null) throw new ArgumentNullException("parent");
            if (socket == null) throw new ArgumentNullException("socket");
            if (requestHandler == null) throw new ArgumentNullException("requestHandler");

            this.Parent = parent;
            parent.AddShutdownAction(this.Stop);

            this._socket = socket;

            _requestHandler = requestHandler;

            Thread = new Thread(this.processNewConnection);
            Thread.Name = threadName ?? string.Format("HandlerThread-{0}", Environment.TickCount);
            Thread.IsBackground = true;
            Thread.Start();
        }

        public void Stop()
        {
            _stopSignal.Set();

            if (Socket != null)
            {
                ShutdownSocket();
            }

            if (Thread.IsAlive)
            {
                log.Debug("thread is alive");
                int time = Environment.TickCount;
                log.Debug("joining thread");
                Thread.Join(1000);
                log.Debug("while Stop()ing, joined for {0} ms", Environment.TickCount - time);
            }
            if (Thread.IsAlive)
            {
                log.Debug("thread is still alive");
                Thread.Abort();
                log.Debug("thread aborted");
            }
        }

        void ShutdownSocket()
        {
            Socket s = Interlocked.Exchange(ref _socket, null);
            if (s != null)
            {
                log.Debug("shutting down the socket");
                s.Shutdown(SocketShutdown.Both);
                log.Debug("closing the socket");
                s.Close();
            }
        }

        void processNewConnection()
        {

            log.Debug("processing new connection...");
            LiterateSocket stream = new LiterateSocket(Socket);

            try
            {
                log.Debug("starting loop");
                bool persistConnection = true;
                while (persistConnection && !_stopSignal.WaitOne(0))
                {

                    log.Debug("calling parseRequest");
                    var ret = parseRequest(stream);
                    log.Debug("returned from parseRequest");

                    if (ret == null)
                    {
                        break;
                    }

                    Request request = ret.Item1;
                    persistConnection = ret.Item2;

                    if (persistConnection &&
                        request.Headers.contains("Connection"))
                    {
                        foreach (var value in request.Headers.findAll("Connection"))
                        {
                            if (value == "close")
                            {
                                persistConnection = false;
                                break;
                            }
                        }
                    }

                    log.Debug("about to handle one request");
                    Response response = _requestHandler(request);
                    log.Debug("handled one request");


                    log.Debug("send the response");
                    sendResponse(stream, response);

                    if (persistConnection &&
                        response.Headers.contains("Connection"))
                    {
                        foreach (var value in response.Headers.findAll("Connection"))
                        {
                            if (value == "close")
                            {
                                persistConnection = false;
                                break;
                            }
                        }
                    }
                }

                log.Debug("ending loop");

            }
            catch (Exception e)
            {
                log.Error("there was an error: {0}", e);
                if (_stopSignal.WaitOne(0) ||
                    (Socket != null &&
                        Socket.IsClosed()))
                {
                    // do nothing
                }
                else
                {
                    sendResponse(stream,
                        new Response(500, "Internal Server Error", null,
                            "The server encountered an unexpected condition which prevented it from fulfilling the request."));
                }
            }
            finally
            {
//                ShutdownSocket();
            }

            log.Debug("done processing");

        }

        Tuple<Request, bool> parseRequest(LiterateSocket stream)
        {

            log.Debug("reading request line");
            var requestLine = stream.ReadLine();

            if (requestLine == null)
            {
                log.Debug("request line is null: {0}", requestLine);

                return null;
            }

            log.Debug("request line is not null: {0}", requestLine);

            var words = requestLine.Split(' ', '\t');
            log.Debug("{0}", words);

            string version;
            string method;
            string path;
            if (words.Length == 3)
            {

                method = words[0];
                path = words[1];
                version = words[2];

                log.Debug("{0}, {1}, {2}", method, path, version);

                if (!version.StartsWith("HTTP/", StringComparison.Ordinal))
                {
                    sendResponse(stream, new Response(400, null, null, string.Format("Bad request version \"{0}\"", version)));
                    return null;
                }

            }
            else
            {

                sendResponse(stream, new Response(400));
                return null;
            }

            log.Debug("checking http protocol version: {0}", version);
            if (version != "HTTP/1.1" &&
                version != "HTTP/1.0" &&
                version != "HTTP/0.9")
            {

                sendResponse(stream, new Response(505, null, null, string.Format("Invalid HTTP Version \"{0}\"}", version)));
                return null;
            }

            HeaderCollection headers = ReadHeaders(stream);

            var persistentConnection = false;
            if (version == "HTTP/1.1")
            {
                persistentConnection = true;
                foreach (var value in headers.findAll("Connection"))
                {
                    if (value == "close")
                    {
                        persistentConnection = false;
                    }
                }
            }

            log.Debug("reading the body");
            var body = ReadBody(stream, headers);

            int length;
            if (body == null)
            {
                length = 0;
            }
            else if (body is byte[])
            {
                length = (body as byte[]).Length;
            }
            else
            {
                length = body.ToString().Length;
            }

            log.Debug("Done reading body, length {0}", length);

            log.Debug("returning");
            return new Tuple<Request, bool>(
                new Request(method, path, headers, body),
                persistentConnection
            );
        }

        void sendResponse(LiterateSocket stream, Response response)
        {
            if (!response.Headers.contains("Server"))
            {
                response.Headers.add("Server", Server.VERSION_STRING);
            }
            if (!response.Headers.contains("Date"))
            {
                response.Headers.add("Date", DateTime.UtcNow.ToString("r"));
            }
            if (response.Body != null)
            {
                int length;
                string contentType;
                if (response.Body is string)
                {
                    length = ((string)response.Body).Length;
                    contentType = "text/plain";
                }
                else if (response.Body is byte[])
                {
                    length = ((byte[])response.Body).Length;
                    contentType = "application/octet-stream";
                }
                else
                {
                    throw new InvalidOperationException("Unknown data type in requestBody");
                }

                if (length > 0)
                {
                    if (!response.Headers.contains("Content-Length"))
                    {
                        response.Headers.add("Content-Length", length);
                    }
                    if (!response.Headers.contains("Content-Type"))
                    {
                        response.Headers.add("Content-Type", contentType);
                    }
                }
            }
            if (!response.Headers.contains("Content-Length") &&
                !response.Headers.contains("Transfer-Encoding"))
            {
                response.Headers.add("Content-Length", 0);
            }

            var writer = stream.Writer;

            var responseLine = string.Format("HTTP/1.1 {0} {1}", response.Code, response.Message ?? string.Empty);
            log.Debug("sending response line: {0}", responseLine);
            writer.Write(responseLine);
            writer.Write("\r\n");

            writer.Flush();

            WriteHeaders(stream, response.Headers);

            WriteBody(response.Body, stream);

            log.Debug("finished sending response");
        }

        public static HeaderCollection ReadHeaders(LiterateSocket inStream)
        {

            log.Debug("reading headers");

            var headers = HeaderCollection.fromReader(inStream);

            foreach (var header in headers)
            {
                log.Debug("  {0}: {1}", header.name, header.value);
            }

            if (headers.size() < 1)
            {
                log.Debug("no headers received");
            }

            return headers;
        }

        public static void WriteHeaders(LiterateSocket outStream, HeaderCollection headers)
        {

            var writer = outStream.Writer;

            log.Debug("Sending headers");
            foreach (Header header in headers.getItems())
            {
                writer.Write("{0}: {1}", header.name, header.value);
                writer.Write("\r\n");
                log.Debug("  \"{0}: {1}\"", header.name, header.value);
            }

            writer.Write("\r\n");
            writer.Flush();
        }


        public static void WriteBody(object body, LiterateSocket outStream, bool chunked = false)
        {

            outStream.Writer.Flush();

            if (body != null)
            {
                if (chunked)
                {

                    WriteBodyChunked(body, outStream);

                }
                else if (body is string)
                {
                    var sbody = (string)body;
                    log.Debug("sending string body, length {0}", sbody.Length);
                    log.Debug(sbody);
                    if (sbody.Length > 0)
                    {
                        var writer = outStream.Writer;
                        writer.Write(sbody);
                        writer.Flush();
                    }

                }
                else if (body is byte[])
                {
                    var bbody = (byte[])body;
                    log.Debug("sending binary body, length {0}", bbody.Length);
                    log.Debug(bbody.ToString());
                    if (bbody.Length > 0)
                    {
                        outStream.Stream.Write(bbody, 0, bbody.Length);
                        outStream.Stream.Flush();
                    }

                }
                else
                {
                    throw new NotSupportedException("Unknown data type in message body");
                }

            }
            else
            {
                log.Debug("No body to send");
            }

            outStream.Stream.Flush();
        }

        public static void WriteBodyChunked(object body, LiterateSocket outStream)
        {

            // see rfc 2616, section 3.6.1

            byte[] buffer;
            if (body is string)
            {
                buffer = Encoding.ASCII.GetBytes(body as string);
            }
            else if (body is byte[])
            {
                buffer = body as byte[];
            }
            else
            {
                throw new NotSupportedException("Unknown data type in message body");
            }

            var writer = outStream.Writer;
            const int maxChunkDataSize = 4096; // in octets
            var bytes = new byte[maxChunkDataSize];
            int i = 0;

            // *chunk
            while (i < buffer.Length)
            {
                int nbytes = Math.Min(buffer.Length - i, maxChunkDataSize);
                // chunk-size
                writer.Write("{0:X}", nbytes);
                // [ chunk-extension ] will go here in the future
                // CRLF
                writer.Write("\r\n");
                writer.Flush();

                // chunk-data
                outStream.Stream.Write(buffer, i, nbytes);
                outStream.Stream.Flush();

                // CRLF
                writer.Write("\r\n");
                writer.Flush();
                i += nbytes;
            }

            // last-chunk
            // 1*("0")
            writer.Write("0");
            // [ chunk-extension ] will go here in the future
            // CRLF
            writer.Write("\r\n");
            writer.Flush();

            // trailer will go here in the future

            // CRLF
            writer.Write("\r\n");
            writer.Flush();
        }


        public static object ReadBody(LiterateSocket inStream, HeaderCollection headers, bool tryConvertToString = true)
        {

            if (headers == null)
                return null;


            if (headers == null)
                return null;

            byte[] bindata = null;

            /*            
               RFC 2616 section 4.4, Message Length
               1. Any response message that must not return a body (1xx, 204,
                    304, HEAD) should be terminated by the first empty line
                    after the header fields, regardless of entity headers.
               2. If the Transfer-Encoding header is present and has a value
                    other than "identity", then it uses chunked encoding.
               3. If Content-Length is present, it specifies both the
                    transfer-length and entity-length in octets (these must be
                    the same)
               4. multipart/byteranges
               5. server closes the connection, for response bodies
             */


            // # 2
            if (headers.contains("Transfer-Encoding"))
            {
                if (headers["Transfer-Encoding"] == "identity")
                {

                    // ignore Transfer-Encoding. proceed to #3

                }
                else if (headers["Transfer-Encoding"] == "chunked")
                {

                    bindata = ReadChunkedBody(inStream);

                }
                else
                {

                    // rfc 2616 § 3.6
                    //
                    // A server which receives an entity-body with a transfer-coding it does
                    // not understand SHOULD return 501 (Unimplemented), and close the
                    // connection. A server MUST NOT send transfer-codings to an HTTP/1.0
                    // client.

                    log.Error("Non-identity transfer encoding, not yet supported in deproxy.  Unable to read response body.");
                    return null;
                }
            }

            // # 3
            if (bindata == null && headers.contains("Content-Length"))
            {
                int length = int.Parse(headers["Content-Length"]);
                log.Debug("Headers contain Content-Length: {0}", length);

                if (length > 0)
                {
                    bindata = new byte[length];
                    int i;
                    int count = 0;
                    log.Debug("  starting to read body");
                    for (i = 0; i < length; i++)
                    {
                        int ii = inStream.Stream.ReadByte();
                        log.Debug("   [{0}] = {1}", i, ii);
                        byte bb = (byte)ii;
                        bindata[i] = bb;
                        count++;
                    }
//            def count = inStream.read(bindata);

                    if (count != length)
                    {
                        // end of stream or some error
                        // TODO: what does the spec say should happen in this case?
                    }
                }
            }

            // # 4 multipart/byteranges

            // else, there is no body (?)

            if (bindata == null)
            {
                log.Debug("Returning null");
                return null;
            }

            if (tryConvertToString)
            {
                // TODO: switch this to true, and always try to read chardata unless
                // it"s a known binary content type
                bool tryCharData = false;

                if (!headers.contains("Content-type"))
                {
                    tryCharData = true;
                }
                else
                {
                    string contentType = headers["Content-Type"];

                    if (contentType != null)
                    {
                        contentType = contentType.ToLower();
                        // use startsWith in order to ignore any charset or other
                        // parameters on the header value
                        if (contentType.StartsWith("text/") ||
                            contentType.StartsWith("application/atom+xml") ||
                            contentType.StartsWith("application/ecmascript") ||
                            contentType.StartsWith("application/json") ||
                            contentType.StartsWith("application/javascript") ||
                            contentType.StartsWith("application/rdf+xml") ||
                            contentType.StartsWith("application/rss+xml") ||
                            contentType.StartsWith("application/soap+xml") ||
                            contentType.StartsWith("application/xhtml+xml") ||
                            contentType.StartsWith("application/xml") ||
                            contentType.StartsWith("application/xml-dtd") ||
                            contentType.StartsWith("application/xop+xml") ||
                            contentType.StartsWith("image/svg+xml") ||
                            contentType.StartsWith("message/http") ||
                            contentType.StartsWith("message/imdn+xml"))
                        {

                            tryCharData = true;
                        }
                    }
                }

                if (tryCharData)
                {
                    string chardata = null;

                    try
                    {
                        chardata = Encoding.ASCII.GetString(bindata);
                    }
                    catch (Exception e)
                    {
                    }

                    if (chardata != null)
                    {
                        return chardata;
                    }
                }
            }

            return bindata;
        }

        static int GetIntegerValue(char c, int radix)
        {
            int val = -1;
            if (char.IsDigit(c))
                val = (int)(c - '0');
            else if (char.IsLower(c))
                val = (int)(c - 'a') + 10;
            else if (char.IsUpper(c))
                val = (int)(c - 'A') + 10;
            else
                throw new ArgumentOutOfRangeException("c", "The value is neither a letter nor a digit.");

            if (val >= radix)
                throw new ArgumentOutOfRangeException("c", "The character is outside the set of acceptable values for the chosen radix.");

            return val;
        }

        public static byte[] ReadChunkedBody(LiterateSocket inStream)
        {

            // see rfc 2616, section 3.6.1

            // Chunked-Body   = *chunk
            //                  last-chunk
            //                  trailer
            //                  CRLF
            //
            // chunk          = chunk-size [ chunk-extension ] CRLF
            //                  chunk-data CRLF
            // chunk-size     = 1*HEX
            // last-chunk     = 1*("0") [ chunk-extension ] CRLF
            //
            // chunk-extension= *( ";" chunk-ext-name [ "=" chunk-ext-val ] )
            // chunk-ext-name = token
            // chunk-ext-val  = token | quoted-string
            // chunk-data     = chunk-size(OCTET)
            // trailer        = *(entity-header CRLF)

            var chunks = new List<byte[]>();

            while (true)
            {

                // chunk-size [ chunk-extension ] CRLF
                // 1*("0") [ chunk-extension ] CRLF
                var line = inStream.ReadLine();

                // find the extent of the chunk-size
                int i;
                for (i = 0; i < line.Length; i++)
                {
                    var value = GetIntegerValue(line[i], 16);
                    if (value < 0 || value > 15)
                        break;
                }

                if (i < 1)
                {
                    // started with an invalid character
                    throw new FormatException("Invalid chunk size");
                }

                int length = Convert.ToInt32(line.Substring(0, i), 16);

                // ignore any chunk-extension for now

                // last-chunk = 1*("0") ...
                if (length < 1)
                    break;

                // chunk-data CRLF
                byte[] chunkData = new byte[length];
                inStream.Stream. Read(chunkData, 0, length);
                inStream.ReadLine();

                chunks.Add(chunkData);
            }

            var trailer = HeaderCollection.fromReader(inStream);
            // we don't do anything with the trailer yet.
            // according to the rfc, everything in the trailer should be an
            // entity-header. There are also additional requirements


            // merge all the chunks together

            int totalLength = 0;
            foreach (byte[] chunk in chunks)
            {
                totalLength += chunk.Length;
            }

            byte[] buffer = new byte[totalLength];

            int index = 0;
            foreach (byte[] chunk in chunks)
            {
                chunk.CopyTo(buffer, index);
                index += chunk.Length;
            }

            return buffer;
        }
    }

}

