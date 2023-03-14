using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test.TestDataClasses.ERP
{
    /// <summary>
    /// Request to test item in item behaviour.
    /// </summary>
    public class TransactionItemRequest : IItemId
    {
        /// <summary>
        /// Type id to map to the right item type.
        /// </summary>
        public Guid? TypeId { get; set; }

        /// <summary>
        /// Gets or sets the items of this transaction.
        /// </summary>
        public IList<TransactionItemRequest>? Items { get; set; }

        /// <summary>
        /// IItem id implementation. Is empty to also test the add cases.
        /// </summary>
        public Guid Id { get; set; } = Guid.Empty;
    }
}
