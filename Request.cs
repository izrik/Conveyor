using System;
using System.Collections.Generic;
using Conveyor.NDeproxy;

namespace Conveyor
{
    public class Request
    {
        public readonly string Method;
        public readonly string Path;
        public readonly HeaderCollection Headers;
        public readonly object Body;

        public Request(string method, string path, HeaderCollection headers = null, object body = null)
        {
            if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("method");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path");

            if (body == null)
            {
                body = "";
            }
            else if (!(body is byte[]) &&
                     !(body is string))
            {
                body = body.ToString();
            }

            this.Method = method;
            this.Path = path;
            this.Headers = headers ?? new HeaderCollection();
            this.Body = body;
        }
    }
}
