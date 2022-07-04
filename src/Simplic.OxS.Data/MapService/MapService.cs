namespace Simplic.OxS.Data
{
    /// <inheritdoc>
    public class MapService : IMapService
    {
        /// <inheritdoc>
        public MergeableObject<T> Create<T>(T original, T target) where T : class
        {
            return new MergeableObject<T>
            {
                Original = original,
                Target = target
            };
        }
    }
}