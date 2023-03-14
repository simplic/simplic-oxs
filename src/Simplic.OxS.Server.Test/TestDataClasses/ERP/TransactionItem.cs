using Simplic.OxS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Test.TestDataClasses.ERP
{
    public class TransactionItem : IItemId
    {
        public Guid Id { get ; set ; }

        public IList<TransactionItem> Items { get; set; } = new List<TransactionItem>();

        public TransactionItemType Type { get; set; }

    }
}
