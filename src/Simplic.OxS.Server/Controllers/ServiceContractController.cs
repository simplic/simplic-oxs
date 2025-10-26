using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Service;
using Simplic.OxS.ServiceDefinition;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;

namespace Simplic.OxS.Server.Controllers;

/// <summary>
/// Controller for managing service contracts
/// </summary>
[Authorize]
[ApiController]
[Route("[Controller]")]
public class ServiceContractController : OxSController
{
    private ServiceDefinitionService serviceDefinitionService;
    private IEndpointContractRepository endpointContractRepository;

    /// <summary>
    /// Create controller instance
    /// </summary>
    /// <param name="serviceDefinitionService">Service definition service</param>
    public ServiceContractController(ServiceDefinitionService serviceDefinitionService, IEndpointContractRepository endpointContractRepository)
    {
        this.serviceDefinitionService = serviceDefinitionService;
        this.endpointContractRepository = endpointContractRepository;
    }

    /// <summary>
    /// Get all organization settings
    /// </summary>
    /// <returns>Collection of organization settings</returns>
    [HttpGet("endpoint-contracts")]
    [ProducesResponseType(typeof(IList<EndpointContract>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> GetServiceContracts()
    {
        return Ok(await endpointContractRepository.GetAllAsync());
    }

    /// <summary>
    /// Get all organization settings
    /// </summary>
    /// <returns>Collection of organization settings</returns>
    [HttpGet("{contractName}/endpoint-contract")]
    [ProducesResponseType(typeof(EndpointContract), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> GetServiceContract([NotNull][Required] string contractName)
    {
        if (string.IsNullOrWhiteSpace(contractName))
            return BadRequest("ContactName must be set");

        var requiredParameter = serviceDefinitionService.ServiceObject.Contract.RequiredEndpointContracts.FirstOrDefault(x => x.Name == contractName);

        if (requiredParameter == null)
            return BadRequest($"Count not find required contract with name: `{contractName}`");

        var contracts = (await endpointContractRepository.GetByFilterAsync(new EndpointContractFilter { Name = contractName, IsDeleted = false })).ToList();

        if (!contracts.Any())
            return NotFound($"Could not find contract: {contractName}");

        return Ok(contracts);
    }

    /// <summary>
    /// Registers the service contract for the current organization
    /// </summary>
    [HttpPost("endpoint-contract")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> SetEndpointContract(SetEndpointContractRequest endpoint)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        if (!serviceDefinitionService.ServiceObject.Contract.RequiredEndpointContracts.Any(x => x.Name == endpoint.ContractName))
            return BadRequest("The given contract is not required by this service.");

        var contract = new EndpointContract
        {
             Name = endpoint.ContractName,
             Endpoint = endpoint.Endpoint
        };

        await endpointContractRepository.UpsertAsync(new EndpointContractFilter { Name = endpoint.ContractName }, contract);
        await endpointContractRepository.CommitAsync();

        return Ok(contract);
    }
}

/// <summary>
/// Request for setting an endpoint contract
/// </summary>
public class SetEndpointContractRequest
{
    /// <summary>
    /// Gets or sets the contract name
    /// </summary>
    [Required]
    [MinLength(5)]
    [NotNull]
    public string ContractName { get; set; }

    /// <summary>
    /// Gets or sets the endpoint URL used to connect to the target service.
    /// [grpc] 
    /// [http.post] 
    /// </summary>
    /// <remarks>The endpoint must be a valid, non-null string. This property is required and cannot be
    /// set to null. Ensure that the endpoint is accessible and properly formatted to avoid connection errors.</remarks>
    [Required]
    [MinLength(5)]
    [NotNull]
    public string Endpoint { get; set; }
}