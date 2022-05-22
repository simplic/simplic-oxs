using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Auth.Model
{
    /// <summary>
    /// Model for requesting a password change. When requesting a password change,
    /// a verification code is required
    /// </summary>
    public class ChangePasswordRequest
    {
        /// <summary>
        /// Gets or sets the new password
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }
    }
}
