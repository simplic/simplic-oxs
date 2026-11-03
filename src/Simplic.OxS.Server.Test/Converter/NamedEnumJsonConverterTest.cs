using Simplic.OxS.Server.Converter;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Server.Test;

public class NamedEnumJsonConverterTest
{
    [JsonConverter(typeof(NamedEnumJsonConverter<TestEnum>))]
    public enum TestEnum
    {
        [EnumMember(Value = "test1")]
        Value1,

        [EnumMember(Value = "test2")]
        Value2,
    }

    [Theory]
    [InlineData("\"test1\"", TestEnum.Value1)]
    [InlineData("\"test2\"", TestEnum.Value2)]
    public void ValidConversion(string json, TestEnum expected)
    {
        var parsed = JsonSerializer.Deserialize<TestEnum>(json);
        parsed.Should().Be(expected);

        var serialized = JsonSerializer.Serialize(expected);
        serialized.Should().Be(json);
    }
}
