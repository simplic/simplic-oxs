using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Test.TestDataClasses.Tour
{
    public class TourModel
    {
        public Guid Id { get; set; }

        public IList<ActionModel> Actions { get; set; }
            = new List<ActionModel>();
    }
}
