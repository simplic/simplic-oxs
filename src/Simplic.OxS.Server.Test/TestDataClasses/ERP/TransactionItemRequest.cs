using Simplic.OxS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Test.TestDataClasses.ERP
{
    public class TransactionItemRequest : IItemId
    {
        public Guid? TypeId { get; set; }

        /// <summary>
        /// Gets or sets the items of this transaction.
        /// </summary>
        public IList<TransactionItemRequest>? Items { get; set; }

        public Guid Id { get; set; } = Guid.Empty;
    }
}
