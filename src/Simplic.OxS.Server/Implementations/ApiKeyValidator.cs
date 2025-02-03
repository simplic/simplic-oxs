using Microsoft.Extensions.Caching.Distributed;
using Simplic.OxS.Server.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Implementations;

internal class ApiKeyValidator(IDistributedCache distributedCache) : IApiKeyValidator
{
    public bool TryValidateApiKey(string apiKey, out Guid? userId, out Guid? organizationId)
    {
        var value = distributedCache.Get(apiKey);

        if (value != null)
        {

            userId = Guid.NewGuid();
            organizationId = Guid.NewGuid();
            return apiKey.Equals("x");
        }
        else
        {
            userId = null;
            organizationId = null;
            return false;
        }
    }
}
