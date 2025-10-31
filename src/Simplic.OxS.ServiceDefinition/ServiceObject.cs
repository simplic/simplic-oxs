namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Represents a service with a set of contracts
/// </summary>
public class ServiceObject
{
    /// <summary>
    /// Gets or sets the name. E.g. logistics
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the version of the sevice. E.g. 1
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the url of the service. E.g. /logistics-api/v1/
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the url of swagger file. E.g. /logistics-api/v1/swagger/v1/swagger.json
    /// </summary>
    public string? SwaggerJsonUrl { get; set; }

    /// <summary>
    /// Gets or sets the url of model definition file /logistics-api/v1/modeldefinition
    /// </summary>
    public string? ModelDefinitionUrl { get; set; }

    /// <summary>
    /// Gets or sets the service type
    /// <list type="bullet">
    /// * internal - service is available inside the ox cluster
    /// </list>
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the contracts inside this service
    /// </summary>
    public IList<ServiceContract> Contracts { get; set; } = [];
}
