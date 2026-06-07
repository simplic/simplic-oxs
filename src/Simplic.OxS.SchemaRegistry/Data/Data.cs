using System;

namespace Simplic.OxS.SchemaRegistry
{
    public interface EventData
    {
        public Guid DataId { get; set; }

        public string JsonData { get; set; }
    }
}
