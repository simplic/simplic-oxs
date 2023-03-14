namespace Simplic.OxS.Server.Test.TestDataClasses.ERP.Simple
{
    public class Transaction
    {
        /// <summary>
        /// Gets or sets the items of this transaction.
        /// </summary>
        public IList<TransactionItem> Items { get; set; } = new List<TransactionItem>();

    }
}
