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
        /// Adds a list of items.
        /// </summary>
        internal IList<PatchConfigurationItem> Items { get; set; }
            = new List<PatchConfigurationItem>();
    }
}
