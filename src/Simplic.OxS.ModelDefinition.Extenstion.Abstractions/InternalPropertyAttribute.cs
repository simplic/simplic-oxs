using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class InternalPropertyAttribute : Attribute
    {
    }
}
