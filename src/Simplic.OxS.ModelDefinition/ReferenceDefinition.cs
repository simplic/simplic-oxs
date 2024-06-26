﻿using System.Globalization;

namespace Simplic.OxS.ModelDefinition
{
    public class ReferenceDefinition
    {
        public string Tilte { get; set; }

        public string Model { get; set; }

        public string SourceUrl { get; set; }

        public OperationDefinition Operation { get; set; }

        public IList<PropertyDefinition> Properties { get; set; }
    }
}