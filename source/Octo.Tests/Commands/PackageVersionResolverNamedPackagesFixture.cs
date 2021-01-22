using NUnit.Framework;
using Octo.Tests.Util;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Infrastructure;
using Serilog;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class PackageVersionResolverNamedPackagesFixture
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

        [Test]
        public void ShouldReturnPackageVersionToUse()
        {
            resolver.Add("PackageA", "Package1", "1.0.0");
            resolver.Add("PackageB", "Package1", "1.1.0");

            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Package1"), Is.EqualTo("1.0.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", "Package1"), Is.EqualTo("1.1.0"));
        }
        
        [Test]
        public void ShouldReturnDockerPackageVersionToUse()
        {
            resolver.Add("PackageA", "Package1", "iamadockertag");
            resolver.Add("PackageB", "Package1", "v1.0.0");

            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Package1"), Is.EqualTo("iamadockertag"));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", "Package1"), Is.EqualTo("v1.0.0"));
        }

        [Test]
        public void ShouldBeCaseInsensitive()
        {
            resolver.Add("PackageA", "Package1", "1.0.0");
            resolver.Add("packageA", "Package1", "1.1.0");

            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Package1"), Is.EqualTo("1.1.0"));
            Assert.That(resolver.ResolveVersion("Step", "packagea", "Package1"), Is.EqualTo("1.1.0"));
        }

        [Test]
        public void ShouldReturnHighestWhenConflicts()
        {
            resolver.Add("PackageA", "Package1", "1.0.0");
            resolver.Add("PackageA", "Package1", "1.1.0");
            resolver.Add("PackageA", "Package1", "0.9.0");

            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Package1"), Is.EqualTo("1.1.0"));
        }

        [Test]
        public void ShouldReturnNullForUnknownSelection()
        {
            resolver.Add("PackageA", "Package1", "1.0.0");

            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Package1"), Is.EqualTo("1.0.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageZ", "Package1"), Is.Null);
        }

        [Test]
        public void ShouldReturnDefaultWhenSet()
        {
            resolver.Default("2.91.0");

            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Package1"), Is.EqualTo("2.91.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", "Package1"), Is.EqualTo("2.91.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageC", "Package1"), Is.EqualTo("2.91.0"));
        }

        [Test]
        public void ShouldParseConstraint()
        {
            resolver.Add("PackageA:Package1:1.0.0");
            resolver.Add("PackageB:Package1:1.0.0-alpha1");
            resolver.Add("PackageB=Package1=1.0.0-alpha1");

            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Package1"), Is.EqualTo("1.0.0"));
            Assert.That(resolver.ResolveVersion("Step", "PackageB", "Package1"), Is.EqualTo("1.0.0-alpha1"));
        }

        [Test]
        public void ShouldParseWildCardConstraint()
        {
            resolver.Add("PackageA:Package2:2.0.0");
            resolver.Add("Step:Package2:3.0.0");
            resolver.Add("PackageA:*:1.0.0");
            resolver.Add("*:Package1:1.0.0-alpha1");
            resolver.Add("*=Package1=1.0.0-alpha1");
            resolver.Add("*=6.0.0");
            resolver.Add("MyStepName:*:v1-0_0.alpha[]");

            // This is an exact match. We prioritise step names over package names
            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Package2"), Is.EqualTo("3.0.0"));
            // This is an exact match to the step name
            Assert.That(resolver.ResolveVersion("Step", "PackageUnknown", "Package2"), Is.EqualTo("3.0.0"));
            // This is an exact match to the package name
            Assert.That(resolver.ResolveVersion("StepUnknown", "PackageA", "Package2"), Is.EqualTo("2.0.0"));
            // This will match the wildcard step but fixed package name version, because we treat the
            // package reference name as more specific
            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Package1"), Is.EqualTo("1.0.0-alpha1"));
            // This will match the fixed step but wildcard package name version
            Assert.That(resolver.ResolveVersion("Step", "PackageA", "Unknown"), Is.EqualTo("1.0.0"));
            // This will also match the wildcard step and fixed package name version, because it is more
            // specific than the default
            Assert.That(resolver.ResolveVersion("Step", "PackageB", "Package1"), Is.EqualTo("1.0.0-alpha1"));
            // This will match the default (i.e. the double wildcard)
            Assert.That(resolver.ResolveVersion("StepWhatever", "PackageB", "PackageUnknown"), Is.EqualTo("6.0.0"));
            // This will match the non-semver version assigned to
            Assert.That(resolver.ResolveVersion("MyStepName", "PackageB", "PackageUnknown"), Is.EqualTo("v1-0_0.alpha[]"));
        }

        [Test]
        public void ShouldPreferStepNameToPackageId()
        {
            resolver.Default("1.0.0");
            resolver.Add("StepName", "Package1", "1.1.0");
            resolver.Add("PackageId", "Package1", "1.2.0");
            Assert.That(resolver.ResolveVersion("StepName", "PackageId", "Package1"), Is.EqualTo("1.1.0"));
        }


        [Test]
        public void ShouldPreferPackageIdToDefault()
        {
            resolver.Default("1.0.0");
            resolver.Add("OtherStep", "Package1", "1.1.0");
            resolver.Add("PackageId", "Package1", "1.2.0");

            Assert.That(resolver.ResolveVersion("StepName", "PackageId", "Package1"), Is.EqualTo("1.2.0"));
        }

        [Test]
        public void ShouldHandleNoStepSpecifiedWithNamedReference()
        {
            resolver.Add("Package1:1.0.0");

            Assert.That(resolver.ResolveVersion("Step", "Package1", "Package1"), Is.EqualTo("1.0.0"));
        }

        [Test]
        public void ShouldHandleNoStepSpecifiedWithDefaultPackageReferenceName()
        {
            resolver.Add("Package1:1.0.0");

            Assert.That(resolver.ResolveVersion("Step", "Package1", null), Is.EqualTo("1.0.0"));
        }

        [Test]
        public void ShouldHandleOnlyStepSpecifiedWithNamedReference()
        {
            resolver.Add("Step:1.0.0");

            Assert.That(resolver.ResolveVersion("Step", "Package1", "Package1"), Is.EqualTo("1.0.0"));
        }

        [Test]
        public void ShouldHandleOnlyStepSpecifiedWithDefaultPackageReferenceName()
        {
            resolver.Add("Step:1.0.0");

            Assert.That(resolver.ResolveVersion("Step", "Package1", null), Is.EqualTo("1.0.0"));
        }

        [Test]
        public void ShouldHandleOnlyStepSpecifiedWithDefaultPackageAndExplicitDefaultSpecified()
        {
            resolver.Add("Step::1.0.0");

            Assert.That(resolver.ResolveVersion("Step", "Package1", null), Is.EqualTo("1.0.0"));
        }

        [Test]
        public void ShouldHandlePackageReferencesOnTheSameStepWithExplicitDefaultPackageReference()
        {
            resolver.Add("Step::1.0.0");
            resolver.Add("Step:foo:1.0.1");

            Assert.That(resolver.ResolveVersion("Step", "Package1", null), Is.EqualTo("1.0.0"));
            Assert.That(resolver.ResolveVersion("Step", "Package1", "foo"), Is.EqualTo("1.0.1"));
        }
    }
}