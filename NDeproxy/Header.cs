using System;
using System.Collections.Generic;

namespace Conveyor.NDeproxy
{
    public class Header
    {
        public readonly string name;
        public readonly string value;

        public Header(string name, string value)
        {
            this.name = name;
            this.value = value;
        }

        public override string ToString()
        {
            return this.name + ": " + this.value;
        }

        public static implicit operator KeyValuePair<string, string>(Header header)
        {
            return new KeyValuePair<string, string>(header.name, header.value);
        }
    }
}
