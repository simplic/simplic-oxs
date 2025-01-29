using Simplic.OxS.Server.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Implementations;

internal class ApiKeyValidator : IApiKeyValidator
{
    public Task<bool> TryValidateApiKeyAsync(string apiKey, out Guid? userId, out Guid? organizationId)
    {
        userId = Guid.NewGuid();
        organizationId = Guid.NewGuid();
        return Task.FromResult(apiKey.Equals("x"));
    }
}
