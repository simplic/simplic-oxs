using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Auth.Model
{
    public class ResetPasswordRequest
    {
        [Required]
        [JsonPropertyName("email")]
        [EmailAddress]
        [MaxLength(255)]
        [MinLength(5)]
        public string EMail { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }
    }
}
