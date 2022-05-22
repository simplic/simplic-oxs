using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Auth.Model
{
    public class RegisterRequest
    {
        [Required]
        [JsonPropertyName("email")]
        [EmailAddress]
        [MaxLength(255)]
        [MinLength(5)]
        public string EMail { get; set; }

        [Phone]
        [Required]
        [MaxLength(255)]
        [MinLength(5)]
        public string PhoneNumber { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
        public string Password { get; set; }

        [Required]
        [Description("'student': user will be handled as student / 'company': user will be handled as company.")]
        public string Type { get; set; }
    }
}
