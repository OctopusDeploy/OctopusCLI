using FluentAssertions;
using NUnit.Framework;

namespace Octo.Tests.Integration
{
    public class ListEnvironmentsTests : IntegrationTestBase
    {
        private const string EnvironmentName = "Foo Environment";

        public ListEnvironmentsTests()
        {

            Get($"{TestRootPath}/api/users/me", p => LoadResponseFile("api/users/me"));
            Get($"{TestRootPath}/api/environments", p => LoadResponseFile(@"api/environments"));
        }

        [Test]
        public void ListEnvironments()
        {
            var result = Execute("list-environments");
            result.LogOutput.Should().Contain(EnvironmentName);
            result.Code.Should().Be(0);
        }
    }
}