using System.Text.Json.Serialization;

namespace Simplic.OxS.Auth.Model
{
    public class RegisterResponse
    {
        [JsonPropertyName("email")]
        public string EMail { get; set; }

        public string Type { get; set; }
    }
}
