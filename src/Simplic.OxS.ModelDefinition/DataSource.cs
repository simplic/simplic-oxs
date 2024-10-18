using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinition
{
    public class DataSource
    {
        public DataSourceType Type { get; set; }

        public string Endpoint { get; set; }

        public string? GqlEntryPoint { get; set; }
    }
}
