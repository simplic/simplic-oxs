using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Hub
{
    [Authorize]
    public abstract class OxSHub<T> : Hub<T> where T : class
    {

    }
}
