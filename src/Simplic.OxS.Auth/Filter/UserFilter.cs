using Simplic.OxS.Data;

namespace Simplic.OxS.Auth
{
    public class UserFilter : IFilter<Guid>
    {
        public Guid Id { get; set; }
        public string EMail { get; set; }
    }
}
