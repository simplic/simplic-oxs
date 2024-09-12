namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions;

/// <summary>
/// Attribute to mark a class as searchable through the search api.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class SearchKeyAttribute : Attribute
{
    private readonly string searchKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchKeyAttribute"/>.
    /// </summary>
    /// <param name="searchKey"></param>
    public SearchKeyAttribute(string searchKey)
    {
        this.searchKey = searchKey;
    }

    /// <summary>
    /// Gets or sets the search key.
    /// </summary>
    public string SearchKey => searchKey;
}
