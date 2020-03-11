using System;
using Octopus.Cli;

namespace Octo
{
    public class Program
    {
        public static int Main(string[] args)
        {
            //args = new []{ "prevent-release-progression", "--server=http://localhost:8065", "--apiKey=API-T8EQW0TKAL2N10LDRS34GNHUS", "--project=TeamCity E2E Check", "-version=0.0.16", "--reason=123" };
            return new CliProgram().Execute(args);
        }
    }
}