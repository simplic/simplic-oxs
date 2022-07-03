using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <inheritdoc>
    public class MapService : IMapService
    {
        /// <inheritdoc>
        public MergeableObject<T> Create<T>(T original, T target) where T : class
        {
            return new MergeableObject<T>
            {
                Original = original,
                Target = target
            };
        }
    }
}
