using System;

namespace Simplic.OxS.Data
{
    public interface IOrganizationIdProvider
    {
        Guid? GetOrganizationId();
    }
}
