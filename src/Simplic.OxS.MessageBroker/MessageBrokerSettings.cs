using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.MessageBroker
{
    /// <summary>
    /// Message broker factory
    /// </summary>
    public class MessageBrokerSettings
    {
        /// <summary>
        /// Gets or sets the host
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the username
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the password
        /// </summary>
        public string Password { get; set; }
    }
}
