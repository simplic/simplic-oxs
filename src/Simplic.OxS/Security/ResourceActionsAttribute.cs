namespace Simplic.OxS.Security
{
    /// <summary>
    /// Defines a list of provided actions for a specific resource
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ResourceActionsAttribute : Attribute
    {
        public ResourceActionsAttribute(string name, string[] actions)
        {
            Name = name;
            Actions = actions;
        }

        /// <summary>
        /// Gets or sets the resource name. E.g. oxs:logistics.orders
        /// </summary>
        public string Name 
        {
            get;
            init;
        }

        /// <summary>
        /// Gets or sets a list of available actions.
        /// For example: read, create, update, delete
        /// </summary>
        public string[] Actions 
        { 
            get; 
            init;
        }
    }
}
