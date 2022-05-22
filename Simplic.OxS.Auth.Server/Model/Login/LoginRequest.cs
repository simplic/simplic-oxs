using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Auth.Model
{
    public class LoginRequest
    {
        [Required]
        [JsonPropertyName("email")]
        [EmailAddress]
        [MaxLength(255)]
        [MinLength(5)]
        public string EMail { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string LoginDevice { get; set; }
    }
}