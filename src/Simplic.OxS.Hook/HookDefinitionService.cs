namespace Simplic.OxS.Hook
{
    public class HookDefinitionService
    {
        public IDictionary<Type, HookDefinitionAttribute> Definitions { get; set; } = new Dictionary<Type, HookDefinitionAttribute>();
    }
}
