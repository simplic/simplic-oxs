using System;
using System.Collections.Generic;

namespace Simplic.OxS.Auth.SchemaRegistry
{
    public interface CreateUserEvent
    {
        Guid Id { get; set; }
        string EMail { get; set; }
        string PhoneNumber { get; set; }
        DateTime? LastLogin { get; set; }
        DateTime RegistrationDate { get; set; }
        int LoginFailed { get; set; }
        IList<string> Roles { get; set; }
    }
}
