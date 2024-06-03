using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinition
{
    public class ModelDefinition
    {
        public string? Title { get; set; }

        public string? Model { get; set; }

        public string SourceUrl { get; set; }

        public Operations Operations { get; set; }
            = new Operations();

        public IList<DataSource> DataSources { get; set; }
            = new List<DataSource>();

        public IList<PropertyDefinition> Properties { get; set; }
            = new List<PropertyDefinition>();

        public IList<ReferenceDefinition> References { get; set; }
            = new List<ReferenceDefinition>();
    }
}
