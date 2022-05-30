using System;
using Simplic.OxS.Data;

namespace Simplic.OxS.Data
{
    public interface IUserIdProvider
    {
        Guid? GetUserId(); 
    }
}
