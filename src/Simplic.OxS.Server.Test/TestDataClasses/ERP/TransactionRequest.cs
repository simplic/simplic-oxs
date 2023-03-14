namespace Simplic.OxS.Server.Test.TestDataClasses.ERP
{
    /// <summary>
    /// Sampe request to test item in item behav
    /// </summary>
    public class TransactionRequest
    {
        /// <summary>
        /// Gets or sets the items of this transaction.
        /// </summary>
        public IList<TransactionItemRequest> Items { get; set; } = new List<TransactionItemRequest>();
    }
}
