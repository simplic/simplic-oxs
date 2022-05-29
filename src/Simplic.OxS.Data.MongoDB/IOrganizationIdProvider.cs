using System;

namespace Simplic.Data.MongoDB
{
    public interface IOrganizationIdProvider
    {
        Guid? GetOrganizationId();
    }
}
