using System.Collections;
using System.Collections.Generic;

namespace Simplic.OxS.Auth.Model
{
    public class TwoFactorResponse
    {
        public IDictionary<string, string> Payload { get; set; }
    }
}
