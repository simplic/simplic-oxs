using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test
{
    public class TestPhoneNumberRequest : IItemId
    {
        public Guid Id { get; set; }

        public string? PhoneNumber { get; set; }

    }
}
