using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data.Test
{
    public class SampleObject : ISampleDefaultDocument
    {
        public int Id { get; set; }

        public string CreateUser { get; set; } = "";

        public DateTime CreateDateTime { get; set; }

        public int Number { get; set; }
    }
}
