using Simplic.OxS.ServiceDefinition;
using System.Collections;
using System.Reflection;

namespace Simplic.OxS.Server.Service;

public static class SchemaGenerator
{
    /// <summary>
    /// Generate a JSON Schema (Draft 2020-12 flavored) for a method's inputs and return type.
    /// Returns a strongly-typed POCO tree (JsonSchema), not a JSON string.
    /// </summary>
    public static JsonSchema GenerateMethodJsonSchema(MethodInfo method, string? title = null)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));

        var visited = new HashSet<Type>();
        var nullCtx = new NullabilityInfoContext();

        // Root object schema
        var root = new JsonSchema
        {
            Schema = "https://json-schema.org/draft/2020-12/schema",
            Title = title ?? $"{method.DeclaringType?.FullName}.{method.Name}",
            SingleType = "object",
            Properties = new Dictionary<string, JsonSchema>()
        };
         
        // parameters
        var parametersSchema = BuildParametersSchema(method, visited, nullCtx);

        // returns
        var returnsSchema = BuildTypeSchema(method.ReturnType, visited, nullCtx);

        root.Properties!["method"] = new JsonSchema
        {
            SingleType = "string",
            Const = method.Name
        };

        root.Properties!["declaringType"] = new JsonSchema
        {
            SingleType = "string",
            Const = method.DeclaringType?.FullName ?? string.Empty
        };

        root.Properties!["parameters"] = parametersSchema;
        root.Properties!["returns"] = returnsSchema;

        root.FinalizeTypes();
        return root;
    }

    // ------------------------ Internals ------------------------

    private static JsonSchema BuildParametersSchema(MethodInfo method, HashSet<Type> visited, NullabilityInfoContext nullCtx)
    {
        var props = new Dictionary<string, JsonSchema>();
        var required = new List<string>();

        foreach (var p in method.GetParameters())
        {
            var paramSchema = BuildTypeSchema(p.ParameterType, visited, nullCtx);

            bool isNullable = IsNullable(p, nullCtx);
            if (!isNullable && !p.IsOptional && !p.HasDefaultValue)
                required.Add(p.Name!);

            props[p.Name!] = paramSchema;
        }

        var obj = new JsonSchema
        {
            SingleType = "object",
            Properties = props
        };

        if (required.Count > 0)
            obj.Required = required.ToArray();

        obj.FinalizeTypes();
        return obj;
    }

    private static JsonSchema BuildTypeSchema(Type type, HashSet<Type> visited, NullabilityInfoContext nullCtx)
    {
        type = UnwrapTask(type);

        // Break cycles for complex reference types
        if (!type.IsPrimitive && type != typeof(string) && !type.IsEnum && !type.IsArray && !IsDictionary(type) && !IsEnumerableLike(type))
        {
            if (visited.Contains(type))
                return new JsonSchema { Ref = $"#/definitions/{type.FullName}" };
        }

        // Nullable<T> value types -> allow null
        var underlyingNullable = Nullable.GetUnderlyingType(type);
        if (underlyingNullable is not null)
        {
            var nn = BuildTypeSchemaNonNullable(underlyingNullable, visited, nullCtx);
            return WithNullable(nn);
        }

        return BuildTypeSchemaNonNullable(type, visited, nullCtx);
    }

    private static JsonSchema BuildTypeSchemaNonNullable(Type type, HashSet<Type> visited, NullabilityInfoContext nullCtx)
    {
        // Simple primitives & specials
        if (type == typeof(string)) return TypeNode("string");
        if (type == typeof(bool)) return TypeNode("boolean");
        if (type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong))
            return TypeNode("integer");
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return TypeNode("number");
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return new JsonSchema { SingleType = "string", Format = "date-time" };
        if (type == typeof(Guid))
            return new JsonSchema { SingleType = "string", Format = "uuid" };
        if (type == typeof(byte[]))
            return new JsonSchema { SingleType = "string", Description = "Base64-encoded bytes" };
        if (type == typeof(void))
            return new JsonSchema { SingleType = "null", Description = "No return value (void)" };

        // Enums as string with enum list
        if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            return new JsonSchema
            {
                SingleType = "string",
                Enum = names,
                Description = $"Enum {type.FullName}"
            };
        }

        // Dictionary<string, TValue>
        if (IsDictionary(type) && TryGetDictionaryTypes(type, out var keyT, out var valT))
        {
            if (keyT != typeof(string))
            {
                return new JsonSchema
                {
                    SingleType = "object",
                    AdditionalProperties = new JsonSchema
                    {
                        Description = $"Unsupported non-string key type: {keyT!.FullName}, value schema omitted"
                    }
                };
            }

            return new JsonSchema
            {
                SingleType = "object",
                AdditionalProperties = BuildTypeSchema(valT!, visited, nullCtx)
            };
        }

        // IEnumerable<T>
        if (IsEnumerableLike(type))
        {
            var elem = GetEnumerableElementType(type) ?? typeof(object);
            return new JsonSchema
            {
                SingleType = "array",
                Items = BuildTypeSchema(elem, visited, nullCtx)
            };
        }

        // Tuples
        if (IsTuple(type, out var tupleArgs))
        {
            var prefix = tupleArgs.Select(t => BuildTypeSchema(t, visited, nullCtx)).ToList();
            return new JsonSchema
            {
                SingleType = "array",
                PrefixItems = prefix,
                MinItems = tupleArgs.Length,
                MaxItems = tupleArgs.Length,
                Description = $"Tuple {type.FullName}"
            };
        }

        // Complex object
        visited.Add(type);

        var properties = new Dictionary<string, JsonSchema>();
        var required = new List<string>();

        var props = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod is not null && p.GetMethod.IsPublic);

        foreach (var p in props)
        {
            var s = BuildTypeSchema(p.PropertyType, visited, nullCtx);
            properties[p.Name] = s;

            bool nullable = IsNullable(p, nullCtx);
            if (!nullable) required.Add(p.Name);
        }

        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var f in fields)
        {
            var s = BuildTypeSchema(f.FieldType, visited, nullCtx);
            properties[f.Name] = s;

            bool nullable = IsNullable(f, nullCtx);
            if (!nullable) required.Add(f.Name);
        }

        var obj = new JsonSchema
        {
            SingleType = "object",
            Properties = properties,
            Description = $"Object {type.FullName}"
        };

        if (required.Count > 0)
            obj.Required = required.ToArray();

        obj.FinalizeTypes();
        return obj;
    }

    private static JsonSchema WithNullable(JsonSchema nonNull)
    {
        nonNull.FinalizeTypes();
        var types = nonNull.Type?.ToList() ?? new List<string>();
        if (!types.Contains("null")) types.Add("null");
        nonNull.Type = types.ToArray();
        return nonNull;
    }

    private static JsonSchema TypeNode(string singleType) => new JsonSchema { SingleType = singleType };

    private static bool IsNullable(ParameterInfo p, NullabilityInfoContext ctx)
    {
        if (p.ParameterType.IsValueType)
            return Nullable.GetUnderlyingType(p.ParameterType) != null;

        try
        {
            var ni = ctx.Create(p);
            return ni.ReadState != NullabilityState.NotNull;
        }
        catch
        {
            return p.IsOptional || p.HasDefaultValue;
        }
    }

    private static bool IsNullable(PropertyInfo p, NullabilityInfoContext ctx)
    {
        if (p.PropertyType.IsValueType)
            return Nullable.GetUnderlyingType(p.PropertyType) != null;

        try
        {
            var ni = ctx.Create(p);
            return ni.ReadState != NullabilityState.NotNull;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsNullable(FieldInfo f, NullabilityInfoContext ctx)
    {
        if (f.FieldType.IsValueType)
            return Nullable.GetUnderlyingType(f.FieldType) != null;

        return true;
    }

    private static bool IsDictionary(Type t) =>
        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)) ||
        (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>));

    private static bool TryGetDictionaryTypes(Type t, out Type? key, out Type? value)
    {
        var dictIface = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>)
            ? t
            : t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (dictIface is not null)
        {
            var args = dictIface.GetGenericArguments();
            key = args[0]; value = args[1];
            return true;
        }

        key = null; value = null;
        return false;
    }

    private static bool IsEnumerableLike(Type t)
    {
        if (t == typeof(string)) return false;
        if (t.IsArray) return true;

        return typeof(IEnumerable).IsAssignableFrom(t) &&
               t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    private static Type? GetEnumerableElementType(Type t)
    {
        if (t.IsArray) return t.GetElementType();

        var ienum = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return ienum?.GetGenericArguments()[0];
    }

    private static bool IsTuple(Type t, out Type[] args)
    {
        args = Type.EmptyTypes;
        if (!t.IsGenericType) return false;

        var def = t.GetGenericTypeDefinition();
        bool valueTuple = def == typeof(ValueTuple<>) ||
                          def == typeof(ValueTuple<,>) ||
                          def == typeof(ValueTuple<,,>) ||
                          def == typeof(ValueTuple<,,,>) ||
                          def == typeof(ValueTuple<,,,,>) ||
                          def == typeof(ValueTuple<,,,,,>) ||
                          def == typeof(ValueTuple<,,,,,,>) ||
                          def == typeof(ValueTuple<,,,,,,,>);
        bool refTuple = def == typeof(Tuple<>) ||
                        def == typeof(Tuple<,>) ||
                        def == typeof(Tuple<,,>) ||
                        def == typeof(Tuple<,,,>) ||
                        def == typeof(Tuple<,,,,>) ||
                        def == typeof(Tuple<,,,,,>) ||
                        def == typeof(Tuple<,,,,,,>) ||
                        def == typeof(Tuple<,,,,,,,>);
        if (valueTuple || refTuple)
        {
            args = t.GetGenericArguments();
            return true;
        }
        return false;
    }

    private static Type UnwrapTask(Type t)
    {
        if (t == typeof(System.Threading.Tasks.Task) || t.FullName == "System.Threading.Tasks.ValueTask")
            return typeof(void);

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
            return t.GetGenericArguments()[0];

        if (t.IsGenericType && t.FullName == "System.Threading.Tasks.ValueTask`1")
            return t.GetGenericArguments()[0];

        return t;
    }
}
