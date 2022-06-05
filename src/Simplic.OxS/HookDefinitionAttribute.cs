using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS
{
    /// <summary>
    /// Marks an interface as hookable
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class HookDefinitionAttribute : Attribute
    {
        /// <summary>
        /// Adds a hook definition
        /// </summary>
        /// <param name="name">Name of the hook</param>
        /// <param name="operation">Hook operation (E.g. created, updated, deleted)</param>
        /// <param name="dataType">Data type name</param>
        public HookDefinitionAttribute([NotNull] string name, [NotNull] string operation, [NotNull] string dataType)
        {
            Name = name;
            Operation = operation;
            DataType = dataType;
        }

        /// <summary>
        /// Gets or sets the name of the hook
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the name of the operation. E.g. created, updated, deleted
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Gets or sets the name of the data type
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// Gets or sets description of the hook
        /// </summary>
        public string? Description { get; set; }
    }
}
