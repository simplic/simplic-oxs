namespace Simplic.OxS.Auth.SchemaRegistry
{
    [HookDefinitionAttribute("User created", "create", "user", Description = "Will be executed if a new user was created.")]
    public interface UserCreatedHook
    {
        Guid Id { get; set; }
        string EMail { get; set; }
        string PhoneNumber { get; set; }
        DateTime RegistrationDate { get; set; }
        IList<string> Roles { get; set; }
    }
}
