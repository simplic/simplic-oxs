namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The entity-to-controller link and the operation slots it publishes.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaOperationsTests
    {
        [Fact]
        public void Build_Operations_FillOneSlotPerEntityAction()
        {
            var operations = SchemaBuild.Degraded.Document.Entry("probe.widget").Operations;

            operations.Should().NotBeNull();
            operations!.Keys.Should().Equal("create", "delete", "get", "replace", "update");
        }

        [Fact]
        public void Build_PatchAndPut_KeepSeparateSlots()
        {
            var operations = SchemaBuild.Degraded.Document.Entry("probe.widget").Operations!;

            operations["update"].Method.Should().Be("PATCH");
            operations["update"].Route.Should().Be("/Widget/{id}");
            operations["replace"].Method.Should().Be("PUT");
            operations["replace"].Route.Should().Be("/Widget/{id}");
        }

        [Fact]
        public void Build_Operations_CarryTheVerbAndRouteOfTheirAction()
        {
            var operations = SchemaBuild.Degraded.Document.Entry("probe.widget").Operations!;

            operations["get"].Method.Should().Be("GET");
            operations["get"].Route.Should().Be("/Widget/{id}");
            operations["create"].Method.Should().Be("POST");
            operations["create"].Route.Should().Be("/Widget");
            operations["delete"].Method.Should().Be("DELETE");
            operations["delete"].Route.Should().Be("/Widget/{id}");
        }

        [Fact]
        public void Build_RoutedActionsThatAreNoEntityOperation_ArePublishedNowhere()
        {
            var operations = SchemaBuild.Degraded.Document.Entry("probe.widget").Operations!;

            var routes = operations.Values.Select(operation => operation.Route);

            routes.Should().NotContain(route => route.Contains("get-all", StringComparison.Ordinal));
            routes.Should().NotContain(route => route.Contains("details", StringComparison.Ordinal));
            routes.Should().NotContain(route => route.Contains("recalculate", StringComparison.Ordinal));
            routes.Should().NotContain(route => route.Contains("bulk", StringComparison.Ordinal));
        }

        [Fact]
        public void Build_LiteralRoutePrefixAndASeparateTemplate_ComposeOneRoute()
        {
            var operations = SchemaBuild.Degraded.Document.Entry("probe.thing").Operations;

            operations.Should().NotBeNull();
            operations!.Keys.Should().Equal("get");
            operations["get"].Method.Should().Be("GET");
            operations["get"].Route.Should().Be("/api/thing-v2/{id}");
        }

        [Fact]
        public void Build_DeclaredSearchKey_LinksAControllerTheDtoConventionCannotMatch()
        {
            var entry = SchemaBuild.Degraded.Document.Entry("probe.widget");

            entry.Operations.Should().NotBeNull();
            entry.Aliases.Should().BeEmpty();
        }

        [Fact]
        public void Build_EntityClaimedByTwoControllers_PublishesNoOperationsAndNoAliases()
        {
            var entry = SchemaBuild.Degraded.Document.Entry("probe.gadget");

            entry.Operations.Should().BeNull();
            entry.Aliases.Should().NotBeNull().And.BeEmpty();
        }

        [Theory]
        [InlineData("probe.link")]
        [InlineData("spare.gadget")]
        [InlineData("badid")]
        public void Build_EntityWithNoController_PublishesNoOperations(string id)
        {
            SchemaBuild.Degraded.Document.Entry(id).Operations.Should().BeNull();
        }

        [Fact]
        public void Build_Operations_AreKeyedInOrdinalOrder()
        {
            var operations = SchemaBuild.Degraded.Document.Entry("probe.widget").Operations!;

            operations.Keys.Should().BeInAscendingOrder(StringComparer.Ordinal);
        }
    }
}
