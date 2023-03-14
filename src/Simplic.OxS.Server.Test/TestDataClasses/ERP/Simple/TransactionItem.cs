using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test.TestDataClasses.ERP.Simple
{
    public class TransactionItem : IItemId
    {
        public Guid Id { get ; set ; }

        public IList<TransactionItem> Items { get; set; } = new List<TransactionItem>();

        public TransactionItemType Type { get; set; }

    }
}
