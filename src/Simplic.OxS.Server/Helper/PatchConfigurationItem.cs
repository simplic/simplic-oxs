namespace Simplic.OxS.Server
{
    /// <summary>
    /// The item for a patch configuration, will contain the path and delegate for the change.
    /// </summary>
    public class PatchConfigurationItem
    {
        private Delegate action { get; set; }

        /// <summary>
        /// Adds an action that should happen
        /// </summary>
        /// <typeparam name="TOriginal"></typeparam>
        /// <typeparam name="TPatch"></typeparam>
        /// <param name="behaviourChange"></param>
        public void ChangeAction<TOriginal, TPatch>(Action<TOriginal, TPatch> behaviourChange)
        {
            action = behaviourChange;
        }

        public void ChangeActionForElements<TOriginal, TPatch>(Action<TOriginal, TPatch> behaviourChange)
        {
            action = behaviourChange;
            IsMemberConfiguration = true;
        }

        /// <summary>
        /// Calls the delegate.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="patch"></param>
        internal void ApplyChange(object original, object patch)
        {
            action.DynamicInvoke(original, patch);
        }

        /// <summary>
        /// The path of the item.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets whether the configuration applies to the element corresponding to the item path or to its collection members.
        /// </summary>
        public bool IsMemberConfiguration { get; private set; }
    }
}
