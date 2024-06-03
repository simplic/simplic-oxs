using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinitionExtension.Test.TestEnv
{
    internal class TestRequest
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [StringLength(50)]
        public string Description { get; set; }

        [Range(0, 100)]
        public int Age { get; set; }

        public bool IsActive { get; set; }

        [Phone]
        public string PhoneNumber { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [FileExtensions(Extensions = "jpg,png")]
        public string FileName { get; set; }

        public DateTime CreatedDate { get; set; }

        public NestedObject NestedObject { get; set; }

        public List<NestedObject> NestedObjects { get; set; }

        public TestEnum Status { get; set; }
    }
}
