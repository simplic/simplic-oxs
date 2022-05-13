using System;
using Simplic.OxS.Data;

namespace Simplic.OxS.Data.MongoDB
{
    public interface IUserIdProvider
    {
        Guid? GetUserId();
    }
}
