using Simplic.OxS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Test
{
    public class TestPhoneNumber : IItemId
    {
        public Guid Id { get; set; }

        public string? PhoneNumber { get; set; }
    }
}
