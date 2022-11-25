using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test
{
    /// <summary>
    /// Represents a employment.
    /// </summary>
    public class EmploymentModel
    {
        /// <summary>
        /// Gets or sets the number.
        /// </summary>
        public string? Number { get; set; }

        /// <summary>
        /// Gets or sets a status if the employment is active.
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Gets or sets the entry date.
        /// </summary>
        public DateTime? EntryDate { get; set; }

        /// <summary>
        /// Gets or sets the exit date.
        /// </summary>
        public DateTime? ExitDate { get; set; }
    }
}