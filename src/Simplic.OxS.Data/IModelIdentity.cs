namespace Simplic.OxS.Data
{
    /// <summary>
    /// Behaviour for getting an id from an object
    /// </summary>
    /// <typeparam name="TId">Type of the id member</typeparam>
    /// <typeparam name="TModel">Type of the model member</typeparam>
    public interface IModelIdentity<TId, TModel>
    {
        /// <summary>
        /// Gets the id of a model
        /// </summary>
        /// <param name="obj">Model to get the id of</param>
        /// <returns>Id value</returns>
        TId GetId(TModel obj);
    }
}
