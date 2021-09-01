using System;
using Octopus.Client.Model;

namespace Octopus.Cli.Model
{
    public class DeploymentRelatedResources
    {
        public string ChannelName { get; set; }
        public ReleaseResource ReleaseResource { get; set; }
    }
}
