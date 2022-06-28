using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Contains an object after and before operations have been applied to it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MergeableObject<T> where T : class
    {
        /// <summary>
        /// The object before any changes have been applied.
        /// </summary>
        public T Original { get; set; }

        /// <summary>
        /// The altered object.
        /// </summary>
        public T Target { get; set; }
    }
}
