using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test.TestDataClasses.ERP.Abstract
{
    public abstract class TransactionItem : IItemId
    {
        public Guid Id { get ; set ; }

        public TransactionItemType Type { get; set; }

    }
}
