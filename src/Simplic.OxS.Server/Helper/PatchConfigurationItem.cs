namespace Simplic.OxS.Server
{
    /// <summary>
    /// The item for a patch configuration, will contain the path and delegate for the change.
    /// </summary>
    public class PatchConfigurationItem
    {
        private Func<object, object, Task> action;

        /// <summary>
        /// Adds an action that should happen
        /// </summary>
        /// <typeparam name="TOriginal"></typeparam>
        /// <typeparam name="TPatch"></typeparam>
        /// <param name="behaviourChange"></param>
        public void ChangeAction<TOriginal, TPatch>(Func<TOriginal, TPatch, Task> behaviourChange)
        {
            action = (o, p) => behaviourChange((TOriginal)o, (TPatch)p);
        }

        /// <summary>
        /// Calls the delegate.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="patch"></param>
        internal async Task ApplyChange(object original, object patch)
        {
            await action.Invoke(original, patch);
        }

        /// <summary>
        /// The path of the item.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the endpath.
        /// </summary>
        public string EndPath { get; set; } = "";
    }
}
