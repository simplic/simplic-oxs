using Simplic.OxS.Data;
using System;

namespace Simplic.OxS.Auth
{
    public class TwoFactorTokenFilter : IFilter<Guid>
    {
        public Guid Id { get; set; }
    }
}