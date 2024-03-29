﻿using System;
using System.Collections.Generic;
using Octopus.Client.Model;

namespace Octopus.Cli.Model
{
    public class ChannelVersionRuleTestResult : Resource
    {
        const string Pass = "PASS";
        const string Fail = "FAIL";
        public IEnumerable<string> Errors { get; set; }
        public bool SatisfiesVersionRange { get; set; }
        public bool SatisfiesPreReleaseTag { get; set; }
        public bool IsSatisfied => SatisfiesVersionRange && SatisfiesPreReleaseTag;
        public bool IsNull { get; private set; }

        public string ToSummaryString()
        {
            return IsNull ? "Allow any version" : $"Range: {(SatisfiesVersionRange ? Pass : Fail)} Tag: {(SatisfiesPreReleaseTag ? Pass : Fail)}";
        }

        public static ChannelVersionRuleTestResult Failed()
        {
            return new ChannelVersionRuleTestResult
            {
                IsNull = false,
                SatisfiesVersionRange = false,
                SatisfiesPreReleaseTag = false
            };
        }

        public static ChannelVersionRuleTestResult Null()
        {
            return new ChannelVersionRuleTestResult
            {
                IsNull = true,
                SatisfiesVersionRange = true,
                SatisfiesPreReleaseTag = true
            };
        }
    }
}
