using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test.TestDataClasses.ERP.Abstract
{
    /// <summary>
    /// Abstract transaction item to test the update behaviour for abstract items and their implementations.
    /// </summary>
    public abstract class TransactionItem : IItemId
    {
        /// <summary>
        /// Implementation for iitem id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Just a type to check whether the right type is added.
        /// </summary>
        public Simple.TransactionItemType Type { get; set; }
    }
}
