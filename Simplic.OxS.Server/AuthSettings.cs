using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simplic.OxS.Server
{
    /// <summary>
    /// Authentication settings
    /// </summary>
    public class AuthSettings
    {
        /// <summary>
        /// Gets or sets the auth token
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the default issuer
        /// </summary>
        public string Issuer { get; set; } = "Academy";
    }
}
