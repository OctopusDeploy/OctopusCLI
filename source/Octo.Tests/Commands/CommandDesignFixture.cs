using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Infrastructure;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class CommandConventionFixture
    {
        [Test]
        public void ShouldBeDecorratedWithTheCommandAttribute()
        {
            var commandClassNamesWithoutCorrectAttribute = Assembly.GetAssembly(typeof(ICommand)).GetTypes()
                .Where(t => t.IsAssignableTo<ICommand>() &&
                            t.GetCustomAttribute<CommandAttribute>() == null &&
                            !t.IsAbstract &&
                            !t.IsInterface)
                .Select(t => t.Name)
                .ToList()
                .AsReadOnly();

            commandClassNamesWithoutCorrectAttribute.Should().BeEmpty($"Each command which implements '{nameof(ICommand)}' interface must be attached with '{nameof(CommandAttribute)}'. " +
                                                                      $"The following command classes: ({string.Join(", ", commandClassNamesWithoutCorrectAttribute)}) are not good enough for you?");
        }

        [Test]
        public void SubClassesOfApiCommand_ShouldEitherOverrideExecuteMethodOrImplementCorrectInterface()
        {
            var commandTypes = Assembly.GetAssembly(typeof(ICommand)).GetTypes()
                .Where(t => typeof(ICommand).IsAssignableFrom(t) &&
                            !t.IsAbstract &&
                            !t.IsInterface &&
                            t.IsSubclassOf(typeof(ApiCommand)))
                .ToList()
                .AsReadOnly();

            var invalidCommandTypes = new List<Type>();

            const string methodName = "Execute";
            foreach (var commandType in commandTypes)
            {
                var isImplementedWithCorrectInterface = typeof(ISupportFormattedOutput).IsAssignableFrom(commandType);
                
                var isMethodOverridenCorrectly = commandType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Any(m => m.Name == methodName &&
                              m.GetBaseDefinition()?.IsVirtual == true &&
                              m.GetBaseDefinition()?.IsFamily == true &&
                              m.IsFamily &&
                              m.GetBaseDefinition()?.DeclaringType != m.DeclaringType);

                if (!isImplementedWithCorrectInterface && !isMethodOverridenCorrectly ||
                    isImplementedWithCorrectInterface && isMethodOverridenCorrectly)
                {
                    invalidCommandTypes.Add(commandType);
                }
            }

            invalidCommandTypes.Should().BeEmpty($"Each command which is a subclass of '{nameof(ApiCommand)}' class must only either override virtual '{methodName}' method Or implement {nameof(ISupportFormattedOutput)} interface. " +
                                                 $"The following command classes: ({string.Join(", ", invalidCommandTypes)}) may require your attention.");
        }
    }
}
