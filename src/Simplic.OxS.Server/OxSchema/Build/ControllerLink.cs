using System.Collections.Immutable;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Simplic.OxS.ModelDefinition.Extenstion.Abstractions;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The link between an entity and the controller that serves it, and everything read off that
    /// link: the entity's legacy model ids and its REST operations.
    /// </summary>
    /// <remarks>
    /// Candidates are the controllers the host publishes model definitions for. The link is
    /// declared where a controller's response type carries <c>[SearchKey("&lt;entity id&gt;")]</c>;
    /// otherwise it is the naming convention <c>&lt;Entity&gt;Model</c> / <c>&lt;Entity&gt;Response</c>
    /// among that controller's declared responses, and an entity two controllers claim that way is
    /// linked to neither.
    /// </remarks>
    internal sealed class ControllerLink
    {
        /// <summary>
        /// Suffixes that turn an entity's CLR name into the name of its controller DTO. Deliberately
        /// not the label suffixes of <see cref="EntityMetadata"/>: both lists reach the wire, and
        /// merging them would change <c>aliases</c> on one side and <c>displayName</c> on the other.
        /// </summary>
        private static readonly string[] ControllerDtoSuffixes = ["Model", "Response"];

        private const string ControllerSuffix = "Controller";
        private const string ControllerToken = "[controller]";

        private readonly IReadOnlyList<Type> controllers;
        private readonly Dictionary<Type, IReadOnlyList<Type>> declaredResponses = [];

        public ControllerLink(IReadOnlyList<Type> controllerTypes)
        {
            controllers =
            [
                .. controllerTypes
                    .Where(controller => controller is not null)
                    .Distinct()
                    .OrderBy(controller => controller.FullName, StringComparer.Ordinal),
            ];
        }

        /// <summary>Entity CLR type to its controller, for the entities that have one.</summary>
        public IReadOnlyDictionary<Type, Type> Link(IReadOnlyList<EntityDeclaration> entities, FindingCollector findings)
        {
            var byId = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var entity in entities)
                byId.TryAdd(entity.Id, entity.ClrType);

            var link = new Dictionary<Type, Type>();

            foreach (var controller in controllers)
                foreach (var response in DeclaredResponses(controller))
                {
                    var declared = response.GetCustomAttribute<SearchKeyAttribute>(inherit: false)?.SearchKey;

                    if (string.IsNullOrWhiteSpace(declared) || !byId.TryGetValue(EntityDiscovery.Normalize(declared), out var entity))
                        continue;

                    // First controller wins, in name order; a second declaration is reported, not honoured.
                    if (!link.TryAdd(entity, controller) && link[entity] != controller)
                        findings.Add(
                            OxSchemaCodes.ControllerLinkAmbiguous,
                            EntityDiscovery.Normalize(declared),
                            "A second controller declares this entity's id, so the first one keeps the link.",
                            $"{link[entity].FullName} keeps it; {controller.FullName} also declares it");
                }

            var claims = new Dictionary<Type, List<Type>>();

            foreach (var controller in controllers)
            {
                var names = DeclaredResponses(controller).Select(response => response.Name).ToHashSet(StringComparer.Ordinal);

                foreach (var entity in entities)
                {
                    if (link.ContainsKey(entity.ClrType))
                        continue;

                    if (!ControllerDtoSuffixes.Any(suffix => names.Contains(entity.ClrType.Name + suffix)))
                        continue;

                    if (!claims.TryGetValue(entity.ClrType, out var claimants))
                        claims[entity.ClrType] = claimants = [];

                    claimants.Add(controller);
                }
            }

            foreach (var entity in entities)
            {
                if (!claims.TryGetValue(entity.ClrType, out var claimants))
                    continue;

                if (claimants.Count == 1)
                    link[entity.ClrType] = claimants[0];
                else
                    findings.Add(
                        OxSchemaCodes.ControllerLinkAmbiguous,
                        entity.Id,
                        $"{claimants.Count} controllers name this entity's DTO, so it is linked to none and publishes no operations.",
                        string.Join(", ", claimants.Select(controller => controller.FullName).OrderBy(name => name, StringComparer.Ordinal)));
            }

            return link;
        }

        /// <summary>
        /// The legacy <c>$ClassName</c> model ids the linked controller publishes for an entity: the
        /// DTO-name convention applied to that controller's declared responses. Deliberately the
        /// convention and not the responses themselves, so a controller linked through a declared
        /// search key whose response is named otherwise publishes operations and no alias; the
        /// aliases are on the wire and a persisted configuration may hold one.
        /// </summary>
        public IReadOnlyList<string> AliasesOf(Type entity, Type? controller)
        {
            if (controller is null)
                return [];

            var names = DeclaredResponses(controller).Select(response => response.Name).ToHashSet(StringComparer.Ordinal);

            return
            [
                .. ControllerDtoSuffixes
                    .Select(suffix => entity.Name + suffix)
                    .Where(names.Contains)
                    .Select(name => "$" + name),
            ];
        }

        /// <summary>
        /// The operations a controller routes, by slot: <c>GET {id}</c>, bare <c>POST</c>,
        /// <c>PATCH {id}</c>, <c>PUT {id}</c> and <c>DELETE {id}</c>. Every other action is not
        /// an entity operation. Null when none of the slots is routed.
        /// </summary>
        public static ImmutableSortedDictionary<string, OxSchemaOperation>? OperationsOf(Type controller)
        {
            var prefix = ControllerRoute(controller);
            var operations = new Dictionary<string, OxSchemaOperation>(StringComparer.Ordinal);

            var methods = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(object))
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ThenBy(method => method.ToString(), StringComparer.Ordinal);

            foreach (var method in methods)
                foreach (var (verb, template) in RoutedVerbs(method))
                {
                    var slot = Slot(verb, template);

                    if (slot is not null)
                        operations.TryAdd(slot, new OxSchemaOperation { Method = verb, Route = Route(prefix, template) });
                }

            return operations.Count == 0 ? null : operations.ToImmutableSortedDictionary(StringComparer.Ordinal);
        }

        /// <summary>The response types a controller's model-definition attributes name, a collection unwrapped to its element.</summary>
        private IReadOnlyList<Type> DeclaredResponses(Type controller)
        {
            if (declaredResponses.TryGetValue(controller, out var cached))
                return cached;

            var responses = new List<Type>();

            foreach (var method in controller
                .GetMethods()
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ThenBy(method => method.ToString(), StringComparer.Ordinal))
            {
                foreach (var response in new[]
                {
                    method.GetCustomAttribute<ModelDefinitionGetOperationAttribute>()?.Response,
                    method.GetCustomAttribute<ModelDefinitionPostOperationAttribute>()?.Response,
                    method.GetCustomAttribute<ModelDefinitionPatchOperationAttribute>()?.Response,
                    method.GetCustomAttribute<ModelDefinitionPutOperationAttribute>()?.Response,
                })
                {
                    if (response is not null)
                        responses.Add(ElementType(response) ?? response);
                }
            }

            return declaredResponses[controller] = responses;
        }

        private static Type? ElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (!type.IsGenericType)
                return null;

            var arguments = type.GetGenericArguments();

            return arguments.Length == 1 && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) ? arguments[0] : null;
        }

        /// <summary>Every verb and action template the routing attributes on one action declare.</summary>
        private static IEnumerable<(string Verb, string Template)> RoutedVerbs(MethodInfo method)
        {
            var routes = method.GetCustomAttributes<RouteAttribute>(inherit: true).Select(route => Trim(route.Template)).ToArray();

            foreach (var http in method.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
            {
                var templates = Trim(http.Template) is { Length: > 0 } own ? [own] : routes.Length > 0 ? routes : [""];

                foreach (var verb in http.HttpMethods)
                    foreach (var template in templates)
                        yield return (verb.ToUpperInvariant(), template);
            }
        }

        private static string? Slot(string verb, string template)
        {
            var bare = template.Length == 0;
            var byId = IsSingleParameter(template);

            return verb switch
            {
                "GET" when byId => "get",
                "POST" when bare => "create",
                "PATCH" when byId => "update",
                "PUT" when byId => "replace",
                "DELETE" when byId => "delete",
                _ => null,
            };
        }

        /// <summary>Whether a template is exactly one route parameter, whatever its name: <c>{id}</c>, <c>{id:guid}</c>.</summary>
        private static bool IsSingleParameter(string template) =>
            template.Length > 2 && template[0] == '{' && template[^1] == '}' && !template.Contains('/', StringComparison.Ordinal);

        /// <summary>The controller's route prefix with the <c>[controller]</c> token expanded; the API base path is not part of it.</summary>
        private static string ControllerRoute(Type controller)
        {
            var name = controller.Name.EndsWith(ControllerSuffix, StringComparison.Ordinal)
                ? controller.Name[..^ControllerSuffix.Length]
                : controller.Name;

            var template = controller
                .GetCustomAttributes<RouteAttribute>(inherit: true)
                .Select(route => route.Template)
                .FirstOrDefault(template => !string.IsNullOrWhiteSpace(template)) ?? name;

            return Trim(template.Replace(ControllerToken, name, StringComparison.Ordinal));
        }

        private static string Route(string prefix, string template)
        {
            var path = prefix.Length == 0 ? template : template.Length == 0 ? prefix : $"{prefix}/{template}";

            return "/" + path;
        }

        private static string Trim(string? template) => template?.Trim().Trim('/') ?? "";
    }
}
