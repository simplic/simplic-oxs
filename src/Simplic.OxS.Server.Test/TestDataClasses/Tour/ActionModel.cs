using Simplic.OxS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Test.TestDataClasses.Tour
{
    public class ActionModel : IItemId
    {
        public Guid Id { get; set; }

        public IList<LoadingSlotModel> UsedLoadingSlots { get; set; }
            = new List<LoadingSlotModel>();
    }
}
