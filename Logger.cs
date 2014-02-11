using System;
using System.Diagnostics;
using System.Text;

namespace Conveyor
{
    public class Logger
    {
        public static readonly StringBuilder _sb = new StringBuilder();

        public enum LogLevel { ERROR = 0, WARN = 1, INFO = 2, DEBUG = 3 };

        public LogLevel LoggingLevel;
        public readonly string Name;

        public Logger(string name)
        {
            Name = name;
        }

        public void Log(LogLevel level, string message, params object[] values)
        {
            if (level >= LoggingLevel)
            {
                string line = string.Format("[{0}] {2}: {1}", level, string.Format(message, values), Name);

                Console.WriteLine(line);
                System.Diagnostics.Debug.WriteLine(line);
                _sb.AppendLine(line);
            }
        }

        public void Debug(string message, params object[] values)
        {
            Log(LogLevel.DEBUG, message, values);
        }

        public void Info(string message, params object[] values)
        {
            Log(LogLevel.INFO, message, values);
        }

        public void Warn(string message, params object[] values)
        {
            Log(LogLevel.WARN, message, values);
        }

        public void Error(string message, params object[] values)
        {
            Log(LogLevel.ERROR, message, values);
        }
    }
}

