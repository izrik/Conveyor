using System;
using System.Collections.Generic;
using Conveyor.NDeproxy;

namespace Conveyor
{
    public class Response
    {
        public readonly int Code;
        public readonly string Message;
        public readonly HeaderCollection Headers;
        public readonly object Body;

        public Response(int code, string message = null, object headers = null, object body = null)
        {

            if (message == null)
            {
                if (HttpResponseMessage.messagesByResponseCode.ContainsKey(code))
                {
                    message = HttpResponseMessage.messagesByResponseCode[code];
                }
                else
                {
                    message = "";
                }
            }

            if (headers == null)
            {
                headers = new Dictionary<string, string>();
            }

            if (body == null)
            {
                body = "";
            }
            else if (!(body is byte[]) &&
                     !(body is string))
            {
                body = body.ToString();
            }

            this.Code = code;
            this.Message = message;
            this.Headers = new HeaderCollection(headers);
            this.Body = body;
        }

        public override string ToString()
        {
            return string.Format("Response(code={0}, message={1}, headers={2}, body={3})", Code, Message, Headers, Body);
        }
    }
}
