namespace Simplic.OxS.Scheduler
{
    /// <summary>
    /// Parameter that can be passed to a scoped hangfire job
    /// </summary>
    public class ScopeJobParameter
    {
        /// <summary>
        /// Gets or sets the organization id that should be used in that job
        /// </summary>
        public Guid? OrganizationId { get; set; }

        /// <summary>
        /// Gets or sets the user id that should be used in that job
        /// </summary>
        public Guid? UserId { get; set; }
    }
}
