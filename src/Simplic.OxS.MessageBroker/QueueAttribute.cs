using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.MessageBroker
{
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueAttribute : Attribute
    {
        public QueueAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
