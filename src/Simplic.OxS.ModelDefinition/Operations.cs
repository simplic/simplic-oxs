using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinition
{
    public class Operations
    {
        public OperationDefinition Create { get; set; }
        public OperationDefinition Update { get; set; }
        public OperationDefinition Delete { get; set; }
        public OperationDefinition Get { get; set; }
    }
}
