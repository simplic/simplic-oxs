namespace Simplic.OxS.Scheduler
{
    /// <summary>
    /// Parameter that can be passed to a scoped hangfire job
    /// </summary>
    public class ScopedJobParameter
    {
        /// <summary>
        /// Gets or sets the organization id that should be used in that job
        /// </summary>
        public Guid? OrganizationId { get; set; }

        /// <summary>
        /// Gets or sets the user id that should be used in that job
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Gets or sets additional / general parameter
        /// </summary>
        public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}
