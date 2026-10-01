namespace Simplic.OxS.Data
{
    /// <summary>
    /// Contains all fluent operations as extension method
    /// </summary>
    public static class FluentTransactionExtension
    {
        /// <summary>
        /// Add a service to the actual fluent builder
        /// </summary>
        /// <typeparam name="K">Service type</typeparam>
        /// <typeparam name="T">Object type</typeparam>
        /// <typeparam name="I">Object unique id type</typeparam>
        /// <param name="builder">Actual builder instance</param>
        /// <param name="service">Service to register/add</param>
        /// <returns>The builder instance that was passed to the method</returns>
        public static IFluentTransactionBuilder AddService<K, T, I>(this IFluentTransactionBuilder builder, K service) where K : ITransactionRepository<T, I>
                                                                                                                       where T : new()
        {
            builder.AddService(service);

            return builder;
        }

        /// <summary>
        /// Calls the create method from a service
        /// </summary>
        /// <typeparam name="K">Service type</typeparam>
        /// <typeparam name="T">Object type</typeparam>
        /// <typeparam name="I">Object unique id type</typeparam>
        /// <param name="builder">Actual builder instance</param>
        /// <param name="func">Delegate for getting the data to create</param>
        /// <returns>The builder instance that was passed to the method</returns>
        public static IFluentTransactionBuilder Create<K, T, I>(this IFluentTransactionBuilder builder, Func<ITransactionRepository<T, I>, T> func) where K : ITransactionRepository<T, I>
                                                                                                                                                    where T : new()
        {
            var service = builder.GetService<T, I>();
            var item = func(service);

            builder.Tasks.Add(async ct => await service.CreateAsync(item, await builder.GetTransaction(ct), ct));

            return builder;
        }

        /// <summary>
        /// Calls the update method from a service
        /// </summary>
        /// <typeparam name="K">Service type</typeparam>
        /// <typeparam name="T">Object type</typeparam>
        /// <typeparam name="I">Object unique id type</typeparam>
        /// <param name="builder">Actual builder instance</param>
        /// <param name="func">Delegate for getting the data to update</param>
        /// <returns>The builder instance that was passed to the method</returns>
        public static IFluentTransactionBuilder Update<K, T, I>(this IFluentTransactionBuilder builder, Func<ITransactionRepository<T, I>, T> func) where K : ITransactionRepository<T, I>
                                                                                                                                                    where T : new()
        {
            var service = builder.GetService<T, I>();
            var item = func(service);

            builder.Tasks.Add(async ct => await service.UpdateAsync(item, await builder.GetTransaction(ct), ct));

            return builder;
        }

        /// <summary>
        /// Calls the delete method from a service
        /// </summary>
        /// <typeparam name="K">Service type</typeparam>
        /// <typeparam name="T">Object type</typeparam>
        /// <typeparam name="I">Object unique id type</typeparam>
        /// <param name="builder">Actual builder instance</param>
        /// <param name="func">Delegate for getting the id of the data to delete</param>
        /// <returns>The builder instance that was passed to the method</returns>
        public static IFluentTransactionBuilder Delete<K, T, I>(this IFluentTransactionBuilder builder, Func<ITransactionRepository<T, I>, I> func) where K : ITransactionRepository<T, I>
                                                                                                                                                    where T : new()
        {
            var service = builder.GetService<T, I>();
            var id = func(service);

            builder.Tasks.Add(async ct => await service.DeleteAsync(id, await builder.GetTransaction(ct), ct));

            return builder;
        }

        /// <summary>
        /// Commit all operations
        /// </summary>
        /// <param name="builder">Actual builder instance</param>
        /// <param name="ct">Cancellation token</param>
        public static async Task CommitAsync(this IFluentTransactionBuilder builder, CancellationToken ct = default)
        {
            foreach (var task in builder.Tasks)
            {
                try
                {
                    await task(ct);
                }
                catch (Exception)
                {
                    // The abort must run even when the caller's token is already cancelled,
                    // otherwise the session stays open with a dangling transaction.
                    await builder.TransactionService.AbortAsync(await builder.GetTransaction(CancellationToken.None), CancellationToken.None);

                    throw;
                }
            }

            await builder.TransactionService.CommitAsync(await builder.GetTransaction(ct), ct);
            builder.Tasks.Clear();
        }

        /// <summary>
        /// Abort the actual transaction and undo changes
        /// </summary>
        /// <param name="builder">Actual builder instance</param>
        /// <param name="ct">Cancellation token</param>
        public static async Task AbortAsync(this IFluentTransactionBuilder builder, CancellationToken ct = default)
        {
            await builder.TransactionService.AbortAsync(await builder.GetTransaction(ct), ct);
            builder.Tasks.Clear();
        }
    }
}
