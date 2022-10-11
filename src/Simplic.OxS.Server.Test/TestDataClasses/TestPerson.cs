using Simplic.OxS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Test
{
    public class TestPerson : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }
    }
}
