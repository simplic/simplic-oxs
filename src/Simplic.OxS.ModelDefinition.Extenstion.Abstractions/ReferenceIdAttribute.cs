using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class ReferenceIdAttribute : Attribute
    {
        private readonly string referenceIdPropertyName;

        public ReferenceIdAttribute(string referenceIdPropertyName)
        {
            this.referenceIdPropertyName = referenceIdPropertyName;
        }

        public string ReferenceIdPropertyName => referenceIdPropertyName;
    }
}
