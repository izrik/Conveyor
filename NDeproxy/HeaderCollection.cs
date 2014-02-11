using System;
using System.Collections.Generic;
using System.IO;
using Conveyor;

namespace Conveyor.NDeproxy
{
    public class HeaderCollection : IEnumerable<Header>, IEnumerable<KeyValuePair<string, string>>
    {
        readonly List<Header> _headers = new List<Header>();

        public HeaderCollection()
        {
        }

        public HeaderCollection(object headers)
        {
            if (headers is Header)
            {
                add(headers as Header);
            }
            else if (headers is KeyValuePair<string, string>)
            {
                var kvp = (KeyValuePair<string, string>)headers;
                add(kvp.Key, kvp.Value);
            }
            else if (headers is string)
            {
                add(SplitString(headers as string));
            }
            else if (headers is IEnumerable<Header>)
            {
                foreach (var header in (IEnumerable<Header>)headers)
                {
                    add(header);
                }
            }
            else if (headers is IEnumerable<KeyValuePair<string, string>>)
            {
                foreach (var kvp in (IEnumerable<KeyValuePair<string, string>>)headers)
                {
                    add(kvp.Key, kvp.Value);
                }
            }
            else if (headers is IEnumerable<string>)
            {
                foreach (var header in (IEnumerable<string>)headers)
                {
                    add(SplitString(header));
                }
            }
            else if (headers != null)
            {
                throw new ArgumentException(string.Format("Unacceptable type: {0}", headers.GetType().FullName), "headers");
            }
        }

        public HeaderCollection(params Header[] headers)
            : this((IEnumerable<Header>)headers)
        {
        }

        public HeaderCollection(params KeyValuePair<string, string>[] headers)
            : this((IEnumerable<KeyValuePair<string, string>>)headers)
        {
        }

        public HeaderCollection(params string[] headers)
            : this((IEnumerable<string>)headers)
        {
        }

        public bool contains(string name)
        {
            foreach (Header header in _headers)
            {
                if (name.EqualsIgnoreCase(header.name))
                {
                    return true;
                }
            }

            return false;
        }

        public int size()
        {
            return _headers.Count;
        }

        public void add(Object name, Object value)
        {
            add(new Header(name.ToString(), value.ToString()));
        }

        public void add(string name, string value)
        {
            add(new Header(name, value));
        }

        public void add(Header header)
        {
            _headers.Add(header);
        }

        public int getCountByName(string name)
        {

            int count = 0;

            foreach (Header header in _headers)
            {
                if (header.name.EqualsIgnoreCase(name))
                {
                    count++;
                }
            }

            return count;
        }

        public IEnumerable<string> findAll(string name)
        {

            var values = new List<string>();

            foreach (Header header in _headers)
            {
                if (header.name.EqualsIgnoreCase(name))
                {
                    yield return header.value;
                }
            }
        }

        public void deleteAll(string name)
        {
            _headers.RemoveAll(header => name.EqualsIgnoreCase(header.name));
        }

        public string[] getNames()
        {
            List<string> names = new List<string>();
            foreach (Header header in _headers)
            {
                names.Add(header.name);
            }

            return names.ToArray();
        }

        public string[] getValues()
        {
            List<string> values = new List<string>();
            foreach (Header header in _headers)
            {
                values.Add(header.value);
            }

            return values.ToArray();
        }

        public Header[] getItems()
        {
            return _headers.ToArray();
        }

        public string getAt(string name)
        {
            return getFirstValue(name);
        }

        public string this [ string name ]
        {
            get { return getFirstValue(name); }
        }

        public Header getAt(int index)
        {
            return _headers[index];
        }

        public Header this [ int index ]
        {
            get { return _headers[index]; }
        }

        public string getFirstValue(string name)
        {
            return getFirstValue(name, null);
        }

        public string getFirstValue(string name, string defaultValue)
        {
            foreach (Header header in _headers)
            {
                if (name.EqualsIgnoreCase(header.name))
                {
                    return header.value;
                }
            }

            return defaultValue;
        }

        public static Header SplitString(string s)
        {
            var parts = s.Split(new [] { ':' }, 2);
            string name = parts[0].Trim();
            string value = (parts.Length > 1 ? parts[1].Trim() : "");

            return new Header(name, value);
        }

        public static HeaderCollection fromReader(LiterateSocket reader)
        {
            HeaderCollection headers = new HeaderCollection();
            string line = reader.ReadLine();
            while (!string.IsNullOrEmpty(line) && line != "\r\n")
            {
                string[] parts = line.Split(new [] { ':' }, 2);
                string name = parts[0];
                string value = (parts.Length > 1 ? parts[1] : "");
                name = name.Trim();
                line = reader.ReadLine();
                while (line.StartsWith(" ") || line.StartsWith("\t"))
                {
                    // Continuation lines - see RFC 2616, section 4.2
                    value += " " + line;
                    line = reader.ReadLine();
                }
                headers.add(name, value.Trim());
            }
            return headers;

        }

        public override string ToString()
        {
            return _headers.ToString();
        }

        public IEnumerator<Header> GetEnumerator()
        {
            return _headers.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, string>> System.Collections.Generic.IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            foreach (var header in _headers)
            {
                yield return header;
            }
        }
    }
}
