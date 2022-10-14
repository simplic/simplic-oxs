using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server
{
    public class ValidationRequest
    {
        public string Property { get; set; }
        public string Path { get; set; }
        public object Value { get; set; }
    }
}
