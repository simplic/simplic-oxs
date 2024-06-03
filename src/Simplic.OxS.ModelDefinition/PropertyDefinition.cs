using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinition
{
    public class PropertyDefinition
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public string? Description { get; set; }

        public bool Internal { get; set; }

        public bool Required { get; set; }

        public bool Nullable { get; set; }

        public string? EnumType { get; set; }

        public IList<EnumItem>? EnumItems { get; set; }

        public string? Format { get; set; }

        public string? MinValue { get; set; }
        public string? MaxValue { get; set; }

        public string? ArrayType { get; set; }

        public string? ReferenceId { get; set; }

        public IList<string>? AvailableTypes { get; set; }
    }
}
