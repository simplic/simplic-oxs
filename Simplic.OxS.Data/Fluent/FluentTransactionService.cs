namespace Simplic.OxS.Data
{
    /// <inheritdoc />
    public class FluentTransactionService : IFluentTransactionService
    {
        private readonly ITransactionService transactionService;

        /// <summary>
        /// Initialize service
        /// </summary>
        /// <param name="transactionService">Transaction service instance</param>
        public FluentTransactionService(ITransactionService transactionService)
        {
            this.transactionService = transactionService;
        }


        /// <inheritdoc />
        public IFluentTransactionBuilder BeginTransaction()
        {
            return new FluentTransactionBuilder(transactionService);
        }
    }
}
