using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Represents a service providing efficient syntax for updating a model using a request object that only contains values for properties that are allowed to be updated.
    /// </summary>
    public interface IMapService
    {
        /// <summary>
        /// Creates the <see cref="MergeableObject{T}"/>.
        /// <para>
        /// Use this method first to pass original and mapped request object.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="original">The object before any changes have been applied.</param>
        /// <param name="mapped">The mapped request object.</param>
        /// <returns></returns>
        MergeableObject<T> Create<T>(T original, T mapped) where T : class;
    }
}
