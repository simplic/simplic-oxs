using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server
{
    public class PatchConfigurationItem
    {

        public void Change<TOriginal, TPatch>(Func<TOriginal, TPatch, bool> behaviourChange)
        {

            Delegate = behaviourChange;
        }


        internal void ApplyChange(object original, object patch)
        {
            var x = Delegate.DynamicInvoke(original, patch);
        }

        public string Path { get; set; }

        internal Delegate Delegate { get; set; }
    }
}
