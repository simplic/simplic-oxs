using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Data;
using Simplic.OxS.Server.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ResourceLocking
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class ResourceLockingController : OxSController
    {
        private readonly ResourceLockingService resourceLockingService;

        public ResourceLockingController(ResourceLockingService resourceLockingService)
        {
            this.resourceLockingService = resourceLockingService;
        }

        [HttpPost("create-lock")]
        [ProducesResponseType(typeof(bool), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> CreateLock(Guid resourceId, Guid userId)
        {
            return Ok(resourceLockingService.CreateLock(resourceId, userId));
        }

        [HttpPost("refresh-lock")]
        [ProducesResponseType(typeof(bool), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> RefreshLock(Guid resourceId, Guid userId)
        {
            resourceLockingService.RefreshLock(resourceId, userId);

            return Ok();
        }

        [HttpPost("release-lock")]
        [ProducesResponseType(typeof(bool), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> ReleaseLock(Guid resourceId, Guid userId)
        {
            return Ok(resourceLockingService.ReleaseLock(resourceId, userId));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(bool), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> CheckLocked(Guid resourceId)
        {
            return Ok(resourceLockingService.CheckLocked(resourceId));
        }
    }
}
