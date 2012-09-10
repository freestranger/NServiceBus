using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NServiceBus.Utils.Reflection;

namespace NServiceBus.Hosting.Helpers
{
    internal static class AssemblyListExtensions
    {
        public static IEnumerable<Type> AllTypes(this IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
                foreach (var type in assembly.GetTypes())
                {
                    yield return type;
                }
        }


        public static IEnumerable<Type> AllTypesAssignableTo<T>(this IEnumerable<Assembly> assemblies)
        {
            var type = typeof(T);
            return assemblies.Where(type.Assembly.IsReferencedBy)
                .AllTypes()
                .Where(type.IsAssignableFrom);
        }

        public static IEnumerable<Type> WhereConcrete(this IEnumerable<Type> types)
        {
            return types.Where(x => !x.IsInterface && !x.IsAbstract);
        }

        public static IEnumerable<Type> AllTypesClosing(this IEnumerable<Assembly> assemblies, Type openGenericType, Type genericArg)
        {
            return assemblies.Where(openGenericType.Assembly.IsReferencedBy)
                .AllTypes()
                .Where(type => type.GetGenericallyContainedType(openGenericType, genericArg) != null);
        }

        static bool IsReferencedBy(this Assembly referenceAssembly, Assembly targetAssembly)
        {
            var name = referenceAssembly.GetName().Name;
            return targetAssembly.GetReferencedAssemblies().Any(y => y.Name == name);
        }
    }
}