using System.Text.RegularExpressions;

namespace Octopus.Cli.Tests.Helpers
{
    public static class ApprovalScrubberExtensions
    {
        public static string ScrubApprovalString(this string approval)
        {
            return approval.ScrubCliVersion().ScrubAssembledTimestamps();
        }

        static readonly Regex CliVersionScrubber = new Regex("(?<=Octopus Deploy Command Line Tool, version\\s)[^\\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static string ScrubCliVersion(this string subject)
        {
            return CliVersionScrubber.Replace(subject, "<VERSION>");
        }

        static readonly Regex AssembledTimestampScrubber = new Regex(@"Assembled: (?<assembled>.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static string ScrubAssembledTimestamps(this string subject)
        {
            foreach (Match m in AssembledTimestampScrubber.Matches(subject))
            {
                subject = subject.Replace(m.Groups["assembled"].Value, "<ASSEMBLED-TIMESTAMP>");
            }
            return subject;
        }
    }
}