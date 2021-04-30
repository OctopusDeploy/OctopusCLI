﻿using System;
using System.Threading.Tasks;
using Octopus.Cli.Commands;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;

namespace Octo.Tests.Commands
{
    public class DummyApiCommand : ApiCommand
    {
        string pill;

        public DummyApiCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, IOctopusCliCommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Dummy");
            options.Add<string>("pill=", "Red or Blue. Blue, the story ends. Red, stay in Wonderland and see how deep the rabbit hole goes.", v => pill = v);
            commandOutputProvider.Debug("Pill: " + pill);
        }

        protected override Task Execute()
        {
            return Task.WhenAll();
        }
    }

    [Command("dummy-command", Description = "this is the command's description")]
    public class DummyApiCommandWithFormattedOutputSupport : ApiCommand, ISupportFormattedOutput
    {
        public DummyApiCommandWithFormattedOutputSupport(IOctopusClientFactory clientFactory, IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusCliCommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
        }

        public bool QueryCalled { get; set; }
        public bool PrintDefaultOutputCalled { get; set; }
        public bool PrintJsonOutputCalled { get; set; }

        public Task Request()
        {
            QueryCalled = true;
            return Task.WhenAll();
        }

        public void PrintDefaultOutput()
        {
            PrintDefaultOutputCalled = true;
        }

        public void PrintJsonOutput()
        {
            PrintJsonOutputCalled = true;
        }
    }
}
