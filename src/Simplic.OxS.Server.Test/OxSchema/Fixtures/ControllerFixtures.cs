using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.ModelDefinition.Extenstion.Abstractions;

namespace Simplic.OxS.Server.Test.OxSchema.Fixtures
{
    /// <summary>
    /// The read shape of the widget entity, named so only the declared key can link it.
    /// </summary>
    [SearchKey("probe.widget")]
    public class WidgetView
    {
        public Guid Id { get; set; }
    }

    /// <summary>The read shape of the thing entity, matched by the DTO-name convention alone.</summary>
    public class ThingModel
    {
        public Guid Id { get; set; }

        /// <summary>Publishes the child half of the item collection's two-part legacy id.</summary>
        public List<Slot> Slots { get; set; } = [];
    }

    /// <summary>A read shape two controllers declare, so neither of them can claim its entity.</summary>
    public class GadgetModel
    {
        public Guid Id { get; set; }
    }

    /// <summary>
    /// Every entity operation, plus four routed actions that are none of them.
    /// </summary>
    [Route("[controller]")]
    public class WidgetController
    {
        [ModelDefinitionGetOperation("/Widget/{id}", typeof(WidgetView))]
        [HttpGet("{id}")]
        public void Get(Guid id)
        {
        }

        [HttpPost]
        public void Create(WidgetView request)
        {
        }

        [HttpPatch("{id}")]
        public void Patch(Guid id, WidgetView request)
        {
        }

        [HttpPut("{id}")]
        public void Replace(Guid id, WidgetView request)
        {
        }

        [HttpDelete("{id}")]
        public void Delete(Guid id)
        {
        }

        [HttpGet("get-all")]
        public void GetAll()
        {
        }

        [HttpGet("{id}/details")]
        public void Details(Guid id)
        {
        }

        [HttpPost("recalculate")]
        public void Recalculate()
        {
        }

        [HttpDelete("bulk")]
        public void DeleteBulk()
        {
        }
    }

    /// <summary>
    /// A literal route prefix, and a verb attribute whose template lives on a separate route
    /// attribute.
    /// </summary>
    [Route("api/thing-v2")]
    public class ThingRestController
    {
        [ModelDefinitionGetOperation("/api/thing-v2/{id}", typeof(ThingModel))]
        [HttpGet]
        [Route("{id}")]
        public void Get(Guid id)
        {
        }
    }

    /// <summary>One of two controllers declaring the same read shape.</summary>
    public class GadgetController
    {
        [ModelDefinitionGetOperation("/Gadget/{id}", typeof(GadgetModel))]
        [HttpGet("{id}")]
        public void Get(Guid id)
        {
        }
    }

    /// <summary>The other of two controllers declaring the same read shape.</summary>
    public class GadgetMirrorController
    {
        [ModelDefinitionGetOperation("/GadgetMirror/{id}", typeof(GadgetModel))]
        [HttpGet("{id}")]
        public void Get(Guid id)
        {
        }
    }
}
