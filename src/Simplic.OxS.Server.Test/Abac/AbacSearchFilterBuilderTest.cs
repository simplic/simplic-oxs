using System.Text.Json;
using FluentAssertions;
using Simplic.OxS.ABAC.Ast;
using Simplic.OxS.ABAC.Contract;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Search;
using Xunit;

namespace Simplic.OxS.Server.Test.Abac;

/// <summary>
/// Unit tests for <see cref="AbacSearchFilterBuilder"/> — pure ABAC DSL → Elasticsearch
/// Query DSL translation. No Redis, no HTTP context.
/// </summary>
public class AbacSearchFilterBuilderTest
{
    private static ConditionNode? Parse(string json)
        => ConditionNodeParser.Parse(
            JsonSerializer.Deserialize<Dictionary<string, object>>(json));

    private static JsonElement BuildElement(string conditionJson)
    {
        var filter = AbacSearchFilterBuilder.Build(Parse(conditionJson));
        filter.Should().NotBeNull();
        return JsonDocument.Parse(filter!).RootElement;
    }

    [Fact]
    public void Build_NullNode_ReturnsNull()
    {
        AbacSearchFilterBuilder.Build(null).Should().BeNull();
    }

    [Fact]
    public void Build_Eq_ProducesTerm()
    {
        var root = BuildElement("{\"department\":{\"$eq\":\"finance\"}}");

        root.GetProperty("term").GetProperty("department").GetString().Should().Be("finance");
    }

    [Fact]
    public void Build_Ne_ProducesMustNotTerm()
    {
        var root = BuildElement("{\"status\":{\"$ne\":\"archived\"}}");

        var mustNot = root.GetProperty("bool").GetProperty("must_not");
        mustNot.GetArrayLength().Should().Be(1);
        mustNot[0].GetProperty("term").GetProperty("status").GetString().Should().Be("archived");
    }

    [Fact]
    public void Build_In_ProducesTerms()
    {
        var root = BuildElement("{\"department\":{\"$in\":[\"finance\",\"legal\"]}}");

        var values = root.GetProperty("terms").GetProperty("department")
            .EnumerateArray().Select(e => e.GetString()).ToArray();
        values.Should().BeEquivalentTo("finance", "legal");
    }

    [Fact]
    public void Build_Nin_ProducesMustNotTerms()
    {
        var root = BuildElement("{\"department\":{\"$nin\":[\"hr\"]}}");

        root.GetProperty("bool").GetProperty("must_not")[0]
            .GetProperty("terms").GetProperty("department")[0].GetString().Should().Be("hr");
    }

    [Fact]
    public void Build_Range_ProducesRange()
    {
        var root = BuildElement("{\"classification\":{\"$lte\":30}}");

        root.GetProperty("range").GetProperty("classification").GetProperty("lte").GetInt32().Should().Be(30);
    }

    [Fact]
    public void Build_ImplicitAnd_ProducesBoolFilter()
    {
        var root = BuildElement("{\"department\":{\"$eq\":\"finance\"},\"status\":{\"$ne\":\"archived\"}}");

        root.GetProperty("bool").GetProperty("filter").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void Build_Or_ProducesShouldWithMinimumShouldMatch()
    {
        var root = BuildElement("{\"$or\":[{\"department\":{\"$eq\":\"finance\"}},{\"owner\":{\"$eq\":\"u1\"}}]}");

        var boolBody = root.GetProperty("bool");
        boolBody.GetProperty("should").GetArrayLength().Should().Be(2);
        boolBody.GetProperty("minimum_should_match").GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData("{\"tags\":{\"$contains\":\"x\"}}")]
    [InlineData("{\"tags\":{\"$ncontains\":\"x\"}}")]
    public void Build_UnsupportedOperator_Throws(string conditionJson)
    {
        var act = () => AbacSearchFilterBuilder.Build(Parse(conditionJson));

        act.Should().Throw<AbacUnsupportedSearchOperatorException>();
    }

    // ── Field-path mapping (ABAC concept §18.11, step 5) ──────────────────────

    private static readonly IReadOnlyDictionary<string, AbacFieldMapping> Mappings =
        new Dictionary<string, AbacFieldMapping>
        {
            ["department"] = new() { PolicyField = "department", SearchPath = "security.attributes.department" },
            ["group"] = new() { PolicyField = "group", SearchPath = "security.principals", SearchValuePrefix = "group" },
            ["clearance"] = new() { PolicyField = "clearance", SearchPath = "security.classification" },
            ["secret"] = new() { PolicyField = "secret", SearchPath = null }
        };

    private static JsonElement BuildMappedElement(string conditionJson)
    {
        var filter = AbacSearchFilterBuilder.Build(Parse(conditionJson), Mappings);
        filter.Should().NotBeNull();
        return JsonDocument.Parse(filter!).RootElement;
    }

    [Fact]
    public void Build_PrincipalFromFieldDefinition_UsesSingleSeparator()
    {
        var set = new Simplic.OxS.ABAC.Mapping.AbacFieldMappingSet
        {
            Fields =
            [
                new() { Field = "department", Type = Simplic.OxS.ABAC.Mapping.AbacFieldType.Principal, SecurityKey = "department" }
            ]
        };

        var filter = AbacSearchFilterBuilder.Build(Parse("""{"department":{"$eq":"finance"}}"""), set.ToFieldMappings());

        filter.Should().Contain("\"department:finance\"").And.NotContain("department::");
    }

    [Fact]
    public void Build_AttributeMapping_RewritesPath()
    {
        var root = BuildMappedElement("{\"department\":{\"$eq\":\"finance\"}}");

        root.GetProperty("term").GetProperty("security.attributes.department").GetString().Should().Be("finance");
    }

    [Fact]
    public void Build_PrincipalMapping_PrefixesValueAndUsesPrincipalsField()
    {
        var root = BuildMappedElement("{\"group\":{\"$eq\":\"accounting\"}}");

        root.GetProperty("term").GetProperty("security.principals").GetString().Should().Be("group:accounting");
    }

    [Fact]
    public void Build_PrincipalMapping_In_PrefixesEachValue()
    {
        var root = BuildMappedElement("{\"group\":{\"$in\":[\"accounting\",\"legal\"]}}");

        var values = root.GetProperty("terms").GetProperty("security.principals")
            .EnumerateArray().Select(e => e.GetString()).ToArray();
        values.Should().BeEquivalentTo("group:accounting", "group:legal");
    }

    [Fact]
    public void Build_ClassificationMapping_Range_RewritesPath()
    {
        var root = BuildMappedElement("{\"clearance\":{\"$lte\":30}}");

        root.GetProperty("range").GetProperty("security.classification").GetProperty("lte").GetInt32().Should().Be(30);
    }

    [Fact]
    public void Build_UnmappedField_WithMappings_FailsClosed()
    {
        var act = () => AbacSearchFilterBuilder.Build(Parse("{\"unknown\":{\"$eq\":\"x\"}}"), Mappings);

        act.Should().Throw<AbacUnsupportedSearchOperatorException>();
    }

    [Fact]
    public void Build_FieldWithoutSearchPath_FailsClosed()
    {
        var act = () => AbacSearchFilterBuilder.Build(Parse("{\"secret\":{\"$eq\":\"x\"}}"), Mappings);

        act.Should().Throw<AbacUnsupportedSearchOperatorException>();
    }

    [Fact]
    public void Build_RangeOnPrincipalField_FailsClosed()
    {
        var act = () => AbacSearchFilterBuilder.Build(Parse("{\"group\":{\"$gte\":\"a\"}}"), Mappings);

        act.Should().Throw<AbacUnsupportedSearchOperatorException>();
    }

    [Fact]
    public void Build_NestedMapping_ResolvesPathsInBoolClauses()
    {
        var root = BuildMappedElement("{\"department\":{\"$eq\":\"finance\"},\"group\":{\"$eq\":\"accounting\"}}");

        var filter = root.GetProperty("bool").GetProperty("filter");
        filter.GetArrayLength().Should().Be(2);
        filter[0].GetProperty("term").GetProperty("security.attributes.department").GetString().Should().Be("finance");
        filter[1].GetProperty("term").GetProperty("security.principals").GetString().Should().Be("group:accounting");
    }
}
