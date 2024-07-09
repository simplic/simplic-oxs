using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Data;
using Simplic.OxS.Data.Service;
using Simplic.OxS.Server.Abstract;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Abstract controller for data deployments.
    /// For the implementation to work only the generic arguments need to be filled AND
    /// it is required to have a mapping from TDocument -> TResponse aswell as from TCreateRequest to TDocument.
    /// </summary>
    /// <typeparam name="TCreateRequest">The type of the deployment create request. This can differ from
    /// a normal create request provided by the api. Also the data contained in the data deployment should match
    /// a json from this request.</typeparam>
    /// <typeparam name="TPatchRequest">The type of the deployment patch request. This can differ from a normal patch
    /// request provided by the api (and also should to limit which fields can be patched with the deployment)
    /// </typeparam>
    /// <typeparam name="TResponse">The response type deriving from <see cref="IDeploymentResponse"/>.</typeparam>
    /// <typeparam name="TService">An organization service base. This can and should be the interface of the service 
    /// since the implementation is provided by the dependency injection anyway.</typeparam>
    /// <typeparam name="TDocument">The api document, this is required to match the right service and should also 
    /// match the TDocument parameter type in TService.</typeparam>
    /// <typeparam name="TFilter">The api document filter, this is required to match the right service and should also 
    /// match the TFilter parameter type in TService.</typeparam>
    public abstract class InternalDeploymentController<
            TCreateRequest,
            TPatchRequest,
            TResponse,
            TService,
            TDocument,
            TFilter>
        : OxSInternalController
        where TResponse : IDeploymentResponse
        where TService : IOrganizationServiceBase<TDocument, TFilter>
        where TFilter : IOrganizationFilter<Guid>
        where TDocument : IOrganizationDocument<Guid>
    {
        private readonly TService service;
        private readonly IMapper mapper;

        /// <summary>
        /// Initializes a new instance of <see cref="InternalDeploymentController{
        /// TCreateRequest, TPatchRequest, TResponse, TService, TDocument, TFilter}"/> with dependency injection.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="mapper"></param>
        protected InternalDeploymentController(TService service, IMapper mapper)
        {
            this.service = service;
            this.mapper = mapper;
        }

        [HttpPost()]
        public virtual async Task<IActionResult> CreateAsync(TCreateRequest createRequest)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await service.Create(mapper.Map<TDocument>(createRequest));

            return Ok(mapper.Map<TResponse>(result));
        }

        [HttpPatch()]
        public virtual async Task<IActionResult> PatchAsync([NotNull] Guid id, [FromBody] TPatchRequest patchRequest)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var original = await service.GetById(id);

            if (original == null)
                return NotFound();

            var patchHelper = new PatchHelper();
            var json = GetRawJson();

            var patch = await patchHelper.Patch(original, patchRequest!, json, (x) => { return true; });

            var result = await service.Update(patch);

            return Ok(mapper.Map<TResponse>(result));
        }
    }
}
