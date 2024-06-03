using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinition.Service
{
    public static class TypeExtensions
    {
        private static readonly HashSet<Type> SimpleTypes = new HashSet<Type>
        {
            typeof(string),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(Guid),
        };

        public static bool IsSimpleType(this Type type)
        {
            return (type.IsValueType && !type.IsEnum) || SimpleTypes.Contains(type) ||
                   (Nullable.GetUnderlyingType(type)?.IsSimpleType() ?? false);
        }

        public static bool IsCollectionType(this Type type)
        {
            if (type.IsArray)
                return true;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return true;

            if (typeof(IEnumerable).IsAssignableFrom(type))
                return true;

            return false;
        }
    }
}
