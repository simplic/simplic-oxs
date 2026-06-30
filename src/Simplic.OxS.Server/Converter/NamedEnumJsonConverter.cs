using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Server.Converter;

/// <summary>
/// Simple json converter for enums that converts enums to a string as defined by the attribute <see cref="EnumMemberAttribute"/>. Falls back to the enum name.
/// </summary>
/// <remarks>
/// Improves usability of enums as part of the api.
/// </remarks>
/// <typeparam name="T">The type of the enum.</typeparam>
public class NamedEnumJsonConverter<T> : JsonConverter<T> where T : struct, Enum
{
    // maps enum name -> enum value
    private readonly Dictionary<string, T> nameToEnum = [];

    // maps enum value -> enum name
    private readonly Dictionary<T, string> enumToName = [];

    /// <summary>
    /// Create a new <see cref="NamedEnumJsonConverter{T}"/>
    /// </summary>
    public NamedEnumJsonConverter()
    {
        var type = typeof(T);
        var values = Enum.GetValues<T>();

        foreach (var value in values)
        {
            var internalName = value.ToString();

            var enumValueMember = type.GetMember(internalName)[0];
            var attribute = enumValueMember.GetCustomAttribute<EnumMemberAttribute>();

            var name = GetName(enumValueMember, attribute);

            nameToEnum.Add(name, value);
            enumToName.Add(value, name);
        }
    }

    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var name = reader.GetString();

        if (name != null && nameToEnum.TryGetValue(name, out var value))
            return value;
        else
            throw new JsonException($"Unknown enum value '{name}'.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (enumToName.TryGetValue(value, out var name))
            writer.WriteStringValue(name);
        else
            throw new InvalidEnumArgumentException(nameof(value), Convert.ToInt32(value), typeof(T));
    }

    /// <summary>
    /// Get the name of the enum value.
    /// </summary>
    protected virtual string GetName(MemberInfo enumMember, EnumMemberAttribute? attribute)
    {
        return attribute?.Value ?? enumMember.Name;
    }
}
