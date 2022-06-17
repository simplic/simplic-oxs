using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data.Test
{
    /// <summary>
    /// This is the interface defining the default properties for SampleObject.
    /// </summary>
    public interface ISampleDefaultDocument : IDefaultDocument
    {
        int Id { get; set; }
        string CreateUser { get; set; }
    }
}
