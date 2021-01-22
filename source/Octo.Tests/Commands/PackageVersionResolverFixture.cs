using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using Octo.Tests.Util;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Infrastructure;
using Serilog;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class PackageVersionResolverFixture
    {
        PackageVersionResolver resolver;
        FakeOctopusFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            var log = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Trace().CreateLogger();
            fileSystem = new FakeOctopusFileSystem();
            resolver = new PackageVersionResolver(log, fileSystem);
        }

        [TestCase("1.0.0", "1.1.0")]
        [TestCase("v1.0.0", "V1.1.0")]
        [TestCase("somedockertag", "blah")]
        public void ShouldReturnPackageVersionToUse(string packageAVersion, string packageBVersion)
        {
            resolver.Add("PackageA", packageAVersion);
            resolver.Add("PackageB", packageBVersion);

            Assert.That(resolver.ResolveVersion("Step", "PackageA"), Is.EqualTo(packageAVersion));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", null), Is.EqualTo(packageAVersion));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", string.Empty), Is.EqualTo(packageAVersion));
            Assert.That(resolver.ResolveVersion("Step", "PackageB"), Is.EqualTo(packageBVersion));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", null), Is.EqualTo(packageBVersion));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", string.Empty), Is.EqualTo(packageBVersion));
        }

        [Test]
        public void ShouldBeCaseInsensitive()
        {
            resolver.Add("PackageA", "1.0.0");
            resolver.Add("packageA", "1.1.0");

            Assert.That(resolver.ResolveVersion("Step", "PackageA"), Is.EqualTo("1.1.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", null), Is.EqualTo("1.1.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", string.Empty), Is.EqualTo("1.1.0"));
            Assert.That(resolver.ResolveVersion("Step", "packagea"), Is.EqualTo("1.1.0"));
            Assert.That(resolver.ResolveVersion("Step", "packagea", null), Is.EqualTo("1.1.0"));
            Assert.That(resolver.ResolveVersion("Step", "packagea", string.Empty), Is.EqualTo("1.1.0"));
        }

        [TestCase("0.9.0", "1.0.0", "1.1.0")]
        [TestCase("v0.9.0", "V1.0.0", "1.1.0")]
        [TestCase("0.9.0", "1.0.0", "v1.1.0")]
        [TestCase("0.9.0-myfeature", "v0.8.0.1", "v0.9.0")]
        public void ShouldReturnHighestWhenConflicts(string version1, string version2, string highestVersion)
        {
            resolver.Add("PackageA", version1);
            resolver.Add("PackageA", version2);
            resolver.Add("PackageA", highestVersion);

            Assert.That(resolver.ResolveVersion("Step", "PackageA"), Is.EqualTo(highestVersion));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", null), Is.EqualTo(highestVersion));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", string.Empty), Is.EqualTo(highestVersion));
        }

        [TestCase("1.0.0")]
        [TestCase("V2.91.0")]
        [TestCase("V2-91_0")]
        public void ShouldReturnNullForUnknownSelection(string version)
        {
            resolver.Add("PackageA", version);

            Assert.That(resolver.ResolveVersion("Step", "PackageA"), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", null), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", string.Empty), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageZ"), Is.Null);
            Assert.That(resolver.ResolveVersion("Step", "PackageZ", null), Is.Null);
            Assert.That(resolver.ResolveVersion("Step", "PackageZ", string.Empty), Is.Null);
        }

        [TestCase("2.91.0")]
        [TestCase("V2.91.0")]
        [TestCase("V2-91_0")]
        public void ShouldReturnDefaultWhenSet(string version)
        {
            resolver.Default(version);

            Assert.That(resolver.ResolveVersion("Step", "PackageA"), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", null), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", string.Empty), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageB"), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", null), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", string.Empty), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageC"), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageC", null), Is.EqualTo(version));
            Assert.That(resolver.ResolveVersion("Step", "PackageC", string.Empty), Is.EqualTo(version));
        }

        [Test]
        public void ShouldParseConstraint()
        {
            resolver.Add("PackageA:1.0.0");
            resolver.Add("PackageB:1.0.0-alpha1");
            resolver.Add("PackageB=1.0.0-alpha1");
            resolver.Add("PackageC=1-0_0.alpha1[]");

            Assert.That(resolver.ResolveVersion("Step", "PackageA"), Is.EqualTo("1.0.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", null), Is.EqualTo("1.0.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageA", string.Empty), Is.EqualTo("1.0.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageB"), Is.EqualTo("1.0.0-alpha1"));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", null), Is.EqualTo("1.0.0-alpha1"));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", string.Empty), Is.EqualTo("1.0.0-alpha1"));
            Assert.That(resolver.ResolveVersion("Step", "PackageC"), Is.EqualTo("1-0_0.alpha1[]"));
            Assert.That(resolver.ResolveVersion("Step", "PackageC", null), Is.EqualTo("1-0_0.alpha1[]"));
            Assert.That(resolver.ResolveVersion("Step", "PackageC", string.Empty), Is.EqualTo("1-0_0.alpha1[]"));
        }

        [Test]
        public void ShouldPreferStepNameToPackageId()
        {
            resolver.Default("1.0.0");
            resolver.Add("StepName", "1.1.0");
            resolver.Add("PackageId", "1.2.0");
            Assert.That(resolver.ResolveVersion("StepName", "PackageId"), Is.EqualTo("1.1.0"));
            Assert.That(resolver.ResolveVersion("StepName", "PackageId", null), Is.EqualTo("1.1.0"));
            Assert.That(resolver.ResolveVersion("StepName", "PackageId", string.Empty), Is.EqualTo("1.1.0"));
        }


        [Test]
        public void ShouldPreferPackageIdToDefault()
        {
            resolver.Default("1.0.0");
            resolver.Add("OtherStep", "1.1.0");
            resolver.Add("PackageId", "1.2.0");

            Assert.That(resolver.ResolveVersion("StepName", "PackageId"), Is.EqualTo("1.2.0"));
            Assert.That(resolver.ResolveVersion("StepName", "PackageId", null), Is.EqualTo("1.2.0"));
            Assert.That(resolver.ResolveVersion("StepName", "PackageId", string.Empty), Is.EqualTo("1.2.0"));
        }


        public static IEnumerable<TestCaseData> CanParseIdAndVersionData()
        {
            var extensions = new[] { ".zip", ".tgz", ".tar.gz", ".tar.Z", ".tar.bz2", ".tar.bz", ".tbz", ".tar" };
            foreach (var ext in extensions)
            {
                yield return CreateCanParseIdAndVersionCase("acme", "1.2.0", ext);
                yield return CreateCanParseIdAndVersionCase("acme", "1.2.0", ext);
                yield return CreateCanParseIdAndVersionCase("acme", "1.2.0.10", ext);
                yield return CreateCanParseIdAndVersionCase("acme", "1.2.0.10", ext);
                yield return CreateCanParseIdAndVersionCase("acme", "1", ext);
                yield return CreateCanParseIdAndVersionCase("acme", "1", ext);
                yield return CreateCanParseIdAndVersionCase("acme", "1.2", ext);
                yield return CreateCanParseIdAndVersionCase("acme.web", "1.2.56", ext);
                yield return CreateCanParseIdAndVersionCase("acme.web", "1.2.0-alpha", ext);
                yield return CreateCanParseIdAndVersionCase("acme.web", "1.2.0-alpha.1.22", ext);
                yield return CreateCanParseIdAndVersionCase("acme.web", "1.2.0-alpha.1.22", ext);
                yield return CreateCanParseIdAndVersionCase("acme.web", "1.2.0+build", ext);
                yield return CreateCanParseIdAndVersionCase("acme.web", "1.2.0+build", ext);
                yield return CreateCanParseIdAndVersionCase("acme.web", "1.2.0-alpha.1+build", ext);
                yield return CreateCanParseIdAndVersionCase("acme.web", "1.2.0-alpha.1+build", ext);
            }

            var invalid = new[]
            {
                "acme+web.1.zip",
                "acme.web.1.0.0.0.0.zip",
                "acme.web-1.0.0.zip"
            };
            yield return new TestCaseData("acme+web.1.zip", false, "acme+web", null).SetName("acme+web.1.zip");
            yield return new TestCaseData("acme.web.1.0.0.0.0.zip", false, "acme.web", null).SetName("acme.web.1.0.0.0.0.zip");
            yield return new TestCaseData("acme.web-1.0.0.zip", false, "acme.web", null).SetName("acme.web-1.0.0.zip");
        }

        private static TestCaseData CreateCanParseIdAndVersionCase(string packageId, string version, string ext)
        {
            var filename = $"{packageId}.{version}{ext}";
            return new TestCaseData(filename, true, packageId, version)
                .SetName(filename);
        }

        [TestCaseSource(nameof(CanParseIdAndVersionData))]
        public void CanParseIdAndVersion(string filename, bool canParse, string expectedPackageId, string expectedVersion)
        {
            var path = Path.Combine("temp", filename);
            fileSystem.Files[path] = "";

            resolver.AddFolder(Path.GetDirectoryName(filename));

            var result = resolver.ResolveVersion("SomeStep", expectedPackageId);
            if (canParse)
                result.Should().Be(expectedVersion);
            else
                result.Should().BeNull();
        }
    }
}