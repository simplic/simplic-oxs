namespace Simplic.OxS.Server.Test.TestDataClasses.ERP.Abstract
{
    /// <summary>
    /// Implementation for a transaction item to test whether items in items can be patched correctly.
    /// </summary>
    public class GroupTransactionItem : TransactionItem
    {
        /// <summary>
        /// A collection of transaction items.
        /// </summary>
        public IList<TransactionItem> Items { get; set; } = new List<TransactionItem>();
    }
}
