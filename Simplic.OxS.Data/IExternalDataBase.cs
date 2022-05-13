using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Interface for repositories with external database access
    /// </summary>
    public interface IExternalDataBase
    {
        /// <summary>
        /// Gets the name of the repository group 
        /// </summary>
        Guid? GroupId { get; }
    }
}
