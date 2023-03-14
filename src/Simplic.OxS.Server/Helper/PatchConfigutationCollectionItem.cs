namespace Simplic.OxS.Server
{
    /// <summary>
    /// The item for a patch collection configuration, will contain the path and delegate for the change.
    /// </summary>
    public class PatchConfigutationCollectionItem
    {
        private Func<object, object> action;
        private Func<object, object> getAsOriginalType;

        /// <summary>
        /// Adds an action that should happen
        /// </summary>
        /// <typeparam name="TPatchItem">The type of the collection item in the patch.</typeparam>
        /// <typeparam name="TNewItem">The type of the new item.</typeparam>
        /// <param name="addItemFunc"></param>
        public void ChangeAddItem<TPatchItem, TNewItem>(Func<TPatchItem, TNewItem> addItemFunc)
        {
            action = (o) => { return addItemFunc((TPatchItem)o); };
        }

        /// <summary>
        /// This method will change the collection handling to take the patched collection as granted without the option
        /// to add or remove specific objects.
        /// </summary>
        public void OverwriteCollectionInPatch<TPatchCollection, TOriginalCollection>(Func<TPatchCollection, TOriginalCollection> getAsOriginalTypeFunc)
        {
            OverwriteCollection = true;
            getAsOriginalType = (o) => { return getAsOriginalTypeFunc((TPatchCollection)o); };
        }

        /// <summary>
        /// Calls the delegate.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="patch"></param>
        internal object GetNewItem(object patch)
        {
            return action.Invoke(patch);
        }

        internal object GetAsOriginalType(object patchCollection)
        {
            return getAsOriginalType.Invoke(patchCollection);
        }

        /// <summary>
        /// The path of the item.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the endpath.
        /// </summary>
        public string EndPath { get; set; } = "";

        internal bool OverwriteCollection { get; set; } = false;
    }
}
