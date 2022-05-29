namespace Simplic.OxS.MessageBroker
{
    public static class Constants
    {
        public class AuthPolicies
        {
            public const string OrganizationUser = "OrganizationUser";
            public const string OrganizationAdmin = "OrganizationAdmin";
        }

        public class ClaimTypes
        {
            public const string Roles = "Roles";
            public const string OrganizationId = "OId";
            public const string OrganizationRoles = "ORoles";
        }
    }
}
