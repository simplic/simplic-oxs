using System;

namespace Simplic.OxS.Auth.Model
{
    public class TwoFactorRequest
    {
        /// <summary>
        /// Gets or sets the token to verify
        /// </summary>
        public Guid TokenId { get; set; }

        /// <summary>
        /// Gets or sets the verification code
        /// </summary>
        public string Code { get; set; }
    }
}
