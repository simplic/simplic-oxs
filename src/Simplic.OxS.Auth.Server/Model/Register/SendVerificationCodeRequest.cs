using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Auth.Model
{
    public class SendVerificationCodeRequest
    {
        /// <summary>
        /// Gets or sets the mail-address to verify
        /// </summary>
        [Required]
        [JsonPropertyName("email")]
        public string EMailAddress { get; set; }
    }
}
