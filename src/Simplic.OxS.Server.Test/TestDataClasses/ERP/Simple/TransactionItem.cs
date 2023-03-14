using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test.TestDataClasses.ERP.Simple
{
    /// <summary>
    /// Item to test item in item patch behaviour.
    /// </summary>
    public class TransactionItem : IItemId
    {
        /// <summary>
        /// Implementation for iitem id.
        /// </summary>
        public Guid Id { get ; set ; }

        /// <summary>
        /// 
        /// </summary>
        public IList<TransactionItem> Items { get; set; } = new List<TransactionItem>();

        public TransactionItemType Type { get; set; }

    }
}
