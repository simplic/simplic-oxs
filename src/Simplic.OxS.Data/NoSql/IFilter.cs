namespace Simplic.OxS.Data
{
    /// <summary>
    /// Represents a basic no sql data filter
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IFilter<TId>
    {
        /// <summary>
        /// Gets or sets the id of the data to filter
        /// </summary>
        TId Id { get; set; }
    }
}