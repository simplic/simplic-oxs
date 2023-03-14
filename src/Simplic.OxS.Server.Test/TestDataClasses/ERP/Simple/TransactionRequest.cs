namespace Simplic.OxS.Server.Test.TestDataClasses.ERP.Simple
{
    public class TransactionRequest
    {
        /// <summary>
        /// Gets or sets the items of this transaction.
        /// </summary>
        public IList<TransactionItemRequest> Items { get; set; } = new List<TransactionItemRequest>();

    }
}
