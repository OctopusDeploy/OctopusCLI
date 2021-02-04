using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Infrastructure;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class CommandConventionFixture
    {
        [TestCaseSource(nameof(Commands))]
        public void AllCommandsShouldBeDecoratedWithTheCommandAttribute(Type commaType)
        {
            commaType.GetCustomAttribute<CommandAttribute>()
                .Should()
                .NotBeNull($"The following type '{commaType.Name}' implements {nameof(ICommand)} " +
                    $"but is not decorated with a {nameof(CommandAttribute)}, which is required for the program to detect it as an available command.");
        }

        [TestCaseSource(nameof(SubclassesOfApiCommand))]
        public void AllSubclassesOfApiCommandShouldEitherOverrideExecuteMethodOrImplementISupportFormattedOutputInterface(Type commandType)
        {
            var isImplementedWithCorrectInterface = typeof(ISupportFormattedOutput).IsAssignableFrom(commandType);

            const string methodName = "Execute";
            var isMethodOverridenCorrectly = commandType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(m => m.Name == methodName &&
                    m.GetBaseDefinition()?.IsVirtual == true &&
                    m.GetBaseDefinition()?.IsFamily == true &&
                    m.IsFamily &&
                    m.GetBaseDefinition()?.DeclaringType != m.DeclaringType);

            var isImplementedWrongly = !isImplementedWithCorrectInterface && !isMethodOverridenCorrectly ||
                isImplementedWithCorrectInterface && isMethodOverridenCorrectly;

            isImplementedWrongly.Should().BeFalse($"The following type '{commandType.Name}' is a subclass of '{nameof(ApiCommand)}', it must only either overrides virtual '{methodName}' method Or implements {nameof(ISupportFormattedOutput)} interface.");
        }

        static IEnumerable<TestCaseData> Commands()
        {
            return CommandTypes()
                .Select(t => new TestCaseData(t).SetName(t.Name));
        }

        static IEnumerable<TestCaseData> SubclassesOfApiCommand()
        {
            return CommandTypes()
                .Where(t => t.IsSubclassOf(typeof(ApiCommand)))
                .Select(t => new TestCaseData(t).SetName(t.Name));
        }

        static IEnumerable<Type> CommandTypes()
        {
            return Assembly.GetAssembly(typeof(ICommand))
                .GetTypes()
                .Where(t => t.IsAssignableTo<ICommand>() &&
                    !t.IsAbstract &&
                    !t.IsInterface);
        }
    }
}
