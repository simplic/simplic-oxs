using MongoDB.Bson.Serialization.Attributes;
using Simplic.OxS.Data;

namespace Simplic.OxS.Auth
{
    public class User : IDocument<Guid>
    {
        [BsonId]
        public Guid Id { get; set; }
        public string EMail { get; set; }
        public string PhoneNumber { get; set; }
        public bool MailVerified { get; set; }
        public string MailVerificationCode { get; set; }
        public string Salt { get; set; }
        public string Password { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime RegistrationDate { get; set; }
        public int LoginFailed { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public string LoginDevice { get; set; }
        public bool IsDeleted { get; set; }
    }
}
