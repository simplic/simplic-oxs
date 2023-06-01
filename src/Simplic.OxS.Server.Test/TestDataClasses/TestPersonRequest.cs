using Simplic.OxS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Test
{
    public class TestPersonRequest : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public Guid? TestGuid { get; set; }

        public DateTime TestDateTime { get; set; }

        public int? TestInt { get; set; }

        public bool? TestBool { get; set; }

        public double? TestDouble { get; set; }

        public IList<TestPhoneNumberRequest>? PhoneNumbers { get; set; }

        public IList<string>? Tags { get; set; }

        public IDictionary<string, object>? AddonProperties { get; set; }

        public TestPhoneNumberRequest? NotInitializedPhoneNumber { get; set; }

        public IList<Guid>? Items { get; set; }

        public IList<(string, string)>? TestTuple { get; set; }
    }
}
