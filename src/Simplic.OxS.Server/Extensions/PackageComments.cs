using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.OxSchema;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// What the OpenAPI document of a service takes from this package's own XML comments
    /// (<c>Simplic.OxS.Server.xml</c>, which the package's build targets put beside the service's
    /// assemblies): the comments of the models of the schema document and of the actions of the
    /// schema routes, and of nothing else in the assembly.
    /// <para>
    /// The comments are written for a reader of the code and refer to members of it
    /// (<c>&lt;see cref&gt;</c>). A reader of the document knows the wire: a reference to a
    /// member of a model becomes that member's wire name, a reference to a model its schema name,
    /// and a reference to a class of constants the values it lists. Runs of whitespace become
    /// one space, so a description does not depend on how its comment is wrapped.
    /// </para>
    /// <para>
    /// The models of the addon definition routes are described by attributes on their members
    /// and are left out here, as is every other type of the package: what a service's document
    /// says of them does not change with this file.
    /// </para>
    /// </summary>
    internal static partial class PackageComments
    {
        /// <summary>The file, by the name of this assembly.</summary>
        internal const string FileName = "Simplic.OxS.Server.xml";

        /// <summary>The models the schema routes answer with: the roots every described model is reached from.</summary>
        private static readonly Type[] Roots = [typeof(OxSchemaDocument), typeof(OxSchemaProperty)];

        /// <summary>The controllers whose actions are described.</summary>
        private static readonly Type[] Routes = [typeof(SchemaController)];

        /// <summary>
        /// The described models: the roots and every type of this assembly a public property of
        /// one of them holds, directly or as the element of a collection or the value of a map.
        /// </summary>
        internal static IReadOnlySet<Type> Models { get; } = Reach(Roots);

        /// <summary>
        /// Includes the described part of <paramref name="directory"/>'s
        /// <see cref="FileName"/> in the document's comments. A host without the file describes
        /// the models without those sentences; a file that cannot be read is left out with a
        /// warning, and the document is generated all the same.
        /// </summary>
        internal static void Include(SwaggerGenOptions options, string directory, ILogger logger)
        {
            var file = System.IO.Path.Combine(directory, FileName);

            if (!File.Exists(file))
                return;

            XDocument comments;

            try
            {
                comments = XDocument.Load(file, LoadOptions.PreserveWhitespace);
            }
            catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "The OpenAPI document leaves out the comments of {File}: the file cannot be read", FileName);

                return;
            }

            var described = Published(comments);

            options.IncludeXmlComments(() => described);
        }

        /// <summary>
        /// The comments of <paramref name="comments"/> the document takes, in the terms of the
        /// wire: the members of <see cref="Models"/> and the actions of the schema routes, each
        /// with its references rewritten.
        /// </summary>
        internal static XPathDocument Published(XDocument comments)
        {
            var kept = new XElement("members");

            foreach (var member in comments.Root?.Element("members")?.Elements("member") ?? [])
            {
                if (!IsPublished((string?)member.Attribute("name") ?? ""))
                    continue;

                var copy = new XElement(member);

                foreach (var reference in copy.Descendants().Where(element => element.Name.LocalName is "see" or "seealso" or "paramref" or "typeparamref").ToList())
                    reference.ReplaceWith(new XText(Wire(reference)));

                foreach (var text in copy.DescendantNodes().OfType<XText>().Where(text => text.Parent?.Name.LocalName != "code").ToList())
                    text.Value = Whitespace().Replace(text.Value, " ");

                kept.Add(copy);
            }

            using var reader = new XDocument(new XElement("doc", kept)).CreateReader();

            return new XPathDocument(reader, XmlSpace.Preserve);
        }

        /// <summary>
        /// Whether a member id of the comment file (<c>T:…</c>, <c>P:…</c>, <c>M:…</c>) is a
        /// described model or a property of one, or an action of a described controller.
        /// </summary>
        internal static bool IsPublished(string id)
        {
            if (id.Length < 3 || id[1] != ':')
                return false;

            var name = id[2..];

            if (id[0] == 'T')
                return Models.Any(model => model.FullName == name);

            // The declaring type: the id without its parameter list and without its last segment.
            var signature = name.IndexOf('(');
            var member = signature < 0 ? name : name[..signature];
            var dot = member.LastIndexOf('.');
            var declaring = dot < 0 ? "" : member[..dot];

            return id[0] switch
            {
                'P' => Models.Any(model => model.FullName == declaring),
                'M' => Routes.Any(route => route.FullName == declaring && IsAction(route, member[(dot + 1)..])),
                _ => false,
            };
        }

        /// <summary>Whether a controller has an action of that name: a public method that answers an HTTP method.</summary>
        private static bool IsAction(Type controller, string name) =>
            controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(method => method.Name == name && method.GetCustomAttributes<HttpMethodAttribute>().Any());

        /// <summary>
        /// What a reference of a comment reads as in the document: the wire name of a model's
        /// member, the schema name of a model, the values of a class of constants or the value
        /// of one of them, the name of a parameter; and, for a reference to anything else of the
        /// code, the words of its simple name, so no member name of the code is printed.
        /// </summary>
        internal static string Wire(XElement reference)
        {
            if ((string?)reference.Attribute("langword") is { } word)
                return Code(word);

            if ((string?)reference.Attribute("name") is { } parameter)
                return Code(parameter);

            if ((string?)reference.Attribute("cref") is not { Length: > 2 } cref || cref[1] != ':')
                return reference.Value;

            var name = cref[2..];
            var signature = name.IndexOf('(');

            if (signature >= 0)
                name = name[..signature];

            if (TypeOf(name) is { } type)
                return Models.Contains(type) ? Code(type.Name) : Constants(type) is { Count: > 0 } values ? string.Join(", ", values.Select(Code)) : Words(type.Name);

            var dot = name.LastIndexOf('.');
            var declaring = dot < 0 ? null : TypeOf(name[..dot]);
            var simple = name[(dot + 1)..];

            if (declaring is not null && Models.Contains(declaring) && declaring.GetProperty(simple, BindingFlags.Public | BindingFlags.Instance) is { } property)
                return Code(WireName(property));

            if (declaring?.GetField(simple, BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } constant && constant.GetRawConstantValue() is string value)
                return Code(value);

            return Words(simple);
        }

        /// <summary>The name a property of a model has in JSON.</summary>
        internal static string WireName(PropertyInfo property) =>
            property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

        private static string Code(string text) => $"`{text}`";

        /// <summary>A name of the code as words: <c>RequireAuthorization</c> reads "require authorization".</summary>
        private static string Words(string name) => PascalBoundary().Replace(name, " ").ToLowerInvariant();

        private static Type? TypeOf(string fullName) => typeof(PackageComments).Assembly.GetType(fullName, throwOnError: false);

        /// <summary>The values of a static class whose public members are string constants, in declaration order; none for any other type.</summary>
        private static List<string> Constants(Type type) =>
            type is { IsAbstract: true, IsSealed: true }
                ? [.. type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                    .OrderBy(field => field.MetadataToken)
                    .Select(field => (string)field.GetRawConstantValue()!)]
                : [];

        private static HashSet<Type> Reach(IEnumerable<Type> roots)
        {
            var reached = new HashSet<Type>();
            var open = new Stack<Type>(roots);

            while (open.TryPop(out var type))
            {
                if (!reached.Add(type))
                    continue;

                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    foreach (var held in Held(property.PropertyType))
                        if (held.Assembly == typeof(PackageComments).Assembly && !reached.Contains(held))
                            open.Push(held);
            }

            return reached;
        }

        /// <summary>A type and, through every generic argument, the types it is made of.</summary>
        private static IEnumerable<Type> Held(Type type)
        {
            if (type.IsArray)
                return Held(type.GetElementType()!);

            return type.IsGenericType ? type.GetGenericArguments().SelectMany(Held) : [type];
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex Whitespace();

        [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])")]
        private static partial Regex PascalBoundary();
    }
}
