namespace Simplic.OxS.Server
{
    /// <summary>
    /// Configuration object for patch helper.
    /// </summary>
    public class PatchConfiguration
    {
        /// <summary>
        /// Adds a new patch configuration item for the given path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public PatchConfigurationItem ForPath(string path)
        {
            var item = new PatchConfigurationItem()
            {
                Path = path
            };

            Items.Add(item);
            return item;
        }

        /// <summary>
        /// Adds a new patch configuration item for a path that starts and ends with the given parameters.
        /// </summary>
        /// <param name="start">The sta</param>
        /// <returns></returns>
        public PatchConfigurationItem ForPath(string start, string end)
        {
            var item = new PatchConfigurationItem()
            {
                Path = start,
                EndPath = end
            };

            Items.Add(item);
            return item;
        }


        /// <summary>
        /// Adds a new patch configuration item for the given collection path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public PatchConfigutationCollectionItem ForCollectionPath(string path)
        {
            var item = new PatchConfigutationCollectionItem
            {
                Path = path
            };

            CollectionItems.Add(item);
            return item;
        }

        public PatchConfigutationCollectionItem ForCollectionPath(string start, string end)
        {
            var item = new PatchConfigutationCollectionItem
            {
                Path = start,
                EndPath = end
            };

            CollectionItems.Add(item);
            return item;
        }

        /// <summary>
        /// Adds a list of items.
        /// </summary>
        internal IList<PatchConfigurationItem> Items { get; set; }
            = new List<PatchConfigurationItem>();

        /// <summary>
        /// Gets or sets the list of collection items.
        /// </summary>
        internal IList<PatchConfigutationCollectionItem> CollectionItems { get; set; }
            = new List<PatchConfigutationCollectionItem>();
    }
}
