using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Tests.Helpers;
using Octopus.CommandLine;
using Serilog;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class VersionTestFixture
    {
        VersionCommand versionCommand;
        StringWriter output;
        TextWriter originalOutput;
        ICommandOutputProvider commandOutputProvider;
        ILogger logger;

        [SetUp]
        public void SetUp()
        {
            originalOutput = Console.Out;
            output = new StringWriter();
            Console.SetOut(output);

            commandOutputProvider = new CommandOutputProvider("Octo", "1.0.0", logger);
            versionCommand = new VersionCommand(commandOutputProvider);
            logger = new LoggerConfiguration().WriteTo.TextWriter(output).CreateLogger();
        }

        [Test]
        public void ShouldPrintCorrectVersionNumber()
        {
            var filename = Path.Combine(Path.GetDirectoryName(AssemblyPath()), "ExpectedSdkVersion.txt");
            var version = GetVersionFromFile(filename);

            versionCommand.Execute();

            output.ToString()
                .Should()
                .Contain(version);
        }

        static string AssemblyPath()
        {
#if NETFRAMEWORK
            //SYSLIB0012: 'Assembly.CodeBase' is obsolete: 'Assembly.CodeBase and Assembly.EscapedCodeBase are
            ////only included for .NET Framework compatibility. Use Assembly.Location instead.'

            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var root = Uri.UnescapeDataString(uri.Path);
            root = root.Replace('/', Path.DirectorySeparatorChar);
            return root;
#else
            return Assembly.GetExecutingAssembly().Location;
#endif
        }

        string GetVersionFromFile(string versionFilePath)
        {
            using (var reader = new StreamReader(File.OpenRead(versionFilePath)))
            {
                return reader.ReadLine();
            }
        }
    }
}
