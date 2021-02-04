using System;
using System.IO;
using System.Text;

namespace Octopus.Cli.Tests.Helpers
{
    //copied from https://stackoverflow.com/a/11911734/779192
    public class ConsoleWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string value)
        {
            WriteEvent?.Invoke(this, new ConsoleWriterEventArgs(value));
        }

        public override void WriteLine(string value)
        {
            WriteLineEvent?.Invoke(this, new ConsoleWriterEventArgs(value));
        }

        public event EventHandler<ConsoleWriterEventArgs> WriteEvent;
        public event EventHandler<ConsoleWriterEventArgs> WriteLineEvent;

        public class ConsoleWriterEventArgs : EventArgs
        {
            public ConsoleWriterEventArgs(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }
    }
}
