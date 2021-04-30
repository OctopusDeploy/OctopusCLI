using System;
using Octopus.Cli.Infrastructure;
using Octopus.CommandLine.Commands;

// ReSharper disable CheckNamespace
namespace Octopus.Cli.Tests.Helpers
{
    public static class TestCommandExtensions
// ReSharper restore CheckNamespace
    {
        public static void Execute(this ICommand command, params string[] args)
        {
            command.Execute(args).GetAwaiter().GetResult();
        }
    }
}
