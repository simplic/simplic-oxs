using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Hook.Model
{
    public class HookDefinitionResponse
    {
        public IList<HookDefinitionModel> Definitions { get; set; } = new List<HookDefinitionModel>();
    }
}
