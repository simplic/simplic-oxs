using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ModelDefinitionExtension.Test.TestEnv
{
    internal class TestResponse
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [StringLength(100)]
        public string Description { get; set; }

        [Range(1, 200)]
        public int Quantity { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public NestedObject NestedObject { get; set; }

        public List<NestedObject> NestedObjects { get; set; }

        public TestEnum Status { get; set; }
    }
}
