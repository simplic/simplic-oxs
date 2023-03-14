namespace Simplic.OxS.Server.Test.TestDataClasses.ERP.Abstract
{
    public class GroupTransactionItem : TransactionItem
    {
        public IList<TransactionItem> Items { get; set; } = new List<TransactionItem>();
    }
}
