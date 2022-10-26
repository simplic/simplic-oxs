using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server
{
    public class PatchConfiguration
    {

        public PatchConfigurationItem ForPath(string path)
        {
            var item = new PatchConfigurationItem()
            {
                Path = path
            };

            Items.Add(item);
            return item;
        }

        internal IList<PatchConfigurationItem> Items { get; set; }
            = new List<PatchConfigurationItem>();
    }
}
