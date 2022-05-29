using MongoDB.Bson.Serialization.Attributes;
using Simplic.OxS.Data;
using System;
using System.Collections.Generic;

namespace Simplic.OxS.Auth
{
    public class TwoFactorToken : IDocument<Guid>
    {
        [BsonId]
        public Guid Id { get; set; }
        public string Code { get; set; }
        public IDictionary<string, string> Payload { get; set; }
        public string Action { get; set; }
        public DateTime CreateDateTime { get; set; }
        public bool IsDeleted { get; set; }
    }
}
