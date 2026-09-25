using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The routes an entity's operations publish, where the controller spells its route token its own way.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class HardeningOperationsTests
    {
        [Theory]
        [InlineData(typeof(LowerTokenController))]
        [InlineData(typeof(PascalTokenController))]
        [InlineData(typeof(UpperTokenController))]
        public void OperationsOf_TheControllerToken_IsExpandedInAnyCasing(Type controller)
        {
            var name = controller.Name[..^"Controller".Length];

            var operations = ControllerLink.OperationsOf(controller);

            operations.Should().NotBeNull();
            operations!["get"].Route.Should().Be($"/{name}/{{id}}");
            operations["create"].Route.Should().Be($"/{name}");
        }

        [Fact]
        public void OperationsOf_ATokenInsideAPrefix_IsExpandedInPlace()
        {
            var operations = ControllerLink.OperationsOf(typeof(PrefixedTokenController));

            operations!["get"].Route.Should().Be("/api/PrefixedToken/{id}");
        }

        /// <summary>The token as the framework documents it.</summary>
        [Route("[controller]")]
        public class LowerTokenController : TokenActions;

        /// <summary>The token as this package's own controllers spell it.</summary>
        [Route("[Controller]")]
        public class PascalTokenController : TokenActions;

        /// <summary>The token in upper case.</summary>
        [Route("[CONTROLLER]")]
        public class UpperTokenController : TokenActions;

        /// <summary>The token below a literal prefix.</summary>
        [Route("api/[Controller]")]
        public class PrefixedTokenController : TokenActions;

        /// <summary>One read and one create action, enough to publish two slots.</summary>
        public abstract class TokenActions
        {
            /// <summary>The read slot.</summary>
            [HttpGet("{id}")]
            public void Get(Guid id)
            {
            }

            /// <summary>The create slot.</summary>
            [HttpPost]
            public void Create()
            {
            }
        }
    }
}
