using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Auth.Model
{
    public class VerifyMailRequest
    {
        /// <summary>
        /// Gets or sets the mail-address to verify
        /// </summary>
        [Required]
        [JsonPropertyName("email")]
        public string EMail { get; set; }

        /// <summary>
        /// Gets or sets the verification code
        /// </summary>
        [Required]
        public string Code { get; set; }
    }
}
