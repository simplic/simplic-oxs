﻿namespace Simplic.OxS.Server
{
    /// <summary>
    /// The item for a patch collection configuration, will contain the path and delegate for the change.
    /// </summary>
    public class PatchConfigutationCollectionItem
    {
        private Func<object, Task<object>> action;

        /// <summary>
        /// Adds an action that should happen
        /// </summary>
        /// <typeparam name="TPatchItem">The type of the collection item in the patch.</typeparam>
        /// <typeparam name="TNewItem">The type of the new item.</typeparam>
        /// <param name="addItemFunc"></param>
        public void ChangeAddItem<TPatchItem, TNewItem>(Func<TPatchItem, Task<TNewItem>> addItemFunc)
        {
            action = (o) => { return Task.FromResult<object>(addItemFunc((TPatchItem)o)); };
        }

        /// <summary>
        /// Calls the delegate.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="patch"></param>
        internal async Task<object> GetNewItem( object patch)
        {
            return await action.Invoke(patch);
        }

        /// <summary>
        /// The path of the item.
        /// </summary>
        public string Path { get; set; }

    }
}
