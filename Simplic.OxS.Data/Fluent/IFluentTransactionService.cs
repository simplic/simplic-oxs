namespace Simplic.OxS.Data
{
    /// <summary>
    /// Service for creation fluent trunsaction builder instances
    /// </summary>
    public interface IFluentTransactionService
    {
        /// <summary>
        /// Create new transaction builder
        /// </summary>
        /// <returns>Instance of a transaction builder</returns>
        IFluentTransactionBuilder BeginTransaction();
    }
}
