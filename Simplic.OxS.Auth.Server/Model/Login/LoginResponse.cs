using System.ComponentModel;

namespace Simplic.OxS.Auth.Model
{
    public class LoginResponse
    {
        public string Token { get; set; }

        public string ErrorState { get; set; }

        [Description("'jwt': Login completed, no two-factor required / 'two-factor': A second two-factor auth is required")]
        public string TokenType { get; set; }
    }
}
