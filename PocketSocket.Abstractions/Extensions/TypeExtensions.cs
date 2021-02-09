using System;
using System.Linq;

namespace PocketSocket.Abstractions.Extensions
{
    public static class TypeExtensions
    {
        public static bool IsOrDerivesFrom<T>(this Type type)
        {
            return type == typeof(T) || type.IsSubclassOf(typeof(T));
        }

        public static bool ImplementsInterface<T>(this Type type)
        {
            var interfaces = type.GetInterfaces();
            return interfaces.Any(i => i == typeof(T) || i.ImplementsInterface<T>());
        }
    }
}