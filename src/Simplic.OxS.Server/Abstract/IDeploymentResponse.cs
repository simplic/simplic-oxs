namespace Simplic.OxS.Server.Abstract
{
    /// <summary>
    /// Base response required to ensure that the response contains an id.
    /// </summary>
    public interface IDeploymentResponse
    {
        /// <summary>
        /// Gets or sets the id of the data object.
        /// </summary>
        public Guid Id { get; set; }
    }
}
