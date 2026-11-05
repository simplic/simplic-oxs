using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Simplic.OxS.ABAC.Compiled;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.ABAC.Subject;
using Simplic.OxS.Server.Abac;

namespace Simplic.OxS.Server.Test.Abac;

/// <summary>Shared builders for the ABAC runtime tests.</summary>
internal static class AbacTestData
{
    public static IDistributedCache NewDistributedCache()
        => new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

    public static IMemoryCache NewMemoryCache() => new MemoryCache(new MemoryCacheOptions());

    public static IOptions<AbacRuntimeOptions> Options(Action<AbacRuntimeOptions>? configure = null)
    {
        var options = new AbacRuntimeOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    public static AbacSubject Subject(Guid orgId, string[]? roles = null, params (Guid Id, string Name)[] teams) => new()
    {
        OrganizationId = orgId,
        User = new AbacSubjectUser { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Email = "user@example.com" },
        Roles = roles ?? ["viewer"],
        Teams = teams.Select(t => new AbacTeam { Id = t.Id, Name = t.Name }).ToList()
    };

    public static CompiledRule Rule(
        AbacEffect effect,
        string[] actions,
        string? mongoFilter = null,
        string? searchFilter = null,
        IDictionary<string, object>? resourceConditions = null,
        bool? hasResourceConditions = null,
        IDictionary<string, object>? subjectConditions = null,
        string ruleId = "r") => new()
    {
        RuleId = ruleId,
        Effect = effect,
        ActionPatterns = actions,
        SubjectConditions = new RuleConditions { RawJson = subjectConditions },
        ResourceConditions = new RuleConditions { RawJson = resourceConditions },
        MongoDbFilterJson = mongoFilter,
        SearchFilter = searchFilter,
        Metadata = new RuleMetadata
        {
            HasSubjectConditions = subjectConditions != null,
            HasResourceConditions = hasResourceConditions ?? (mongoFilter != null || searchFilter != null || resourceConditions != null)
        }
    };

    public static CompiledPolicy Policy(Guid orgId, string policyId, params CompiledRule[] rules) => new()
    {
        PolicyId = policyId,
        OrganizationId = orgId,
        CompilerVersion = "1",
        SetId = "set-1",
        CompiledRules = rules.ToList()
    };

    public static Dictionary<string, object> Cond(string field, string op, object value)
        => new() { [field] = new Dictionary<string, object> { [op] = value } };
}

/// <summary>Manually advanced <see cref="TimeProvider"/>.</summary>
internal sealed class TestTimeProvider : TimeProvider
{
    public TestTimeProvider(DateTimeOffset now) => Now = now;

    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now;
}
