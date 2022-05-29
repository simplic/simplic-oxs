using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Builds and stores all information for a transaction that is represented in fluent-style
    /// </summary>
    public interface IFluentTransactionBuilder
    {
        /// <summary>
        /// Gets a service from the builder
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <typeparam name="I">Unique id type</typeparam>
        /// <returns>Service instance</returns>
        ITransactionRepository<T, I> GetService<T, I>() where T : new();

        /// <summary>
        /// Adds a service-instance to the current builder
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <typeparam name="I">Unique id type</typeparam>
        /// <param name="service">Service instance</param>
        void AddService<T, I>(ITransactionRepository<T, I> service) where T : new();

        /// <summary>
        /// Creates a new transaction if no transaction is existing. Only one instance
        /// will be created during builder-lifetime.
        /// </summary>
        /// <returns>Transaction instance</returns>
        Task<ITransaction> GetTransaction();

        /// <summary>
        /// Gets the transaction service for executing transaction operations on commit and abort
        /// </summary>
        ITransactionService TransactionService { get; }

        /// <summary>
        /// Gets the actual list of tasks to execute when committing or aborting
        /// </summary>
        IList<Func<Task>> Tasks { get; }
    }
}
