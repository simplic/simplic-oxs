using System.Text;
using System.Text.Json;
using Simplic.OxS.Data;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The metadata derived from one CLR type: wire names, keys, the display path and labels.
    /// Every derivation fails closed: where nothing declares a fact, the member is absent.
    /// </summary>
    internal static class EntityMetadata
    {
        /// <summary>The naming policy that produces the REST response body; every wire name comes from it.</summary>
        public static readonly JsonNamingPolicy WireNames = JsonNamingPolicy.CamelCase;

        /// <summary>The properties that name an instance, in preference order.</summary>
        private static readonly string[] DisplayCandidates = ["name", "matchCode", "number"];

        /// <summary>
        /// Suffixes stripped from a type name before it becomes a label. Deliberately not the DTO
        /// suffixes of <see cref="ControllerLink"/>: both lists reach the wire and must not be merged.
        /// </summary>
        private static readonly string[] LabelSuffixes = ["Model", "Response", "Dto"];

        /// <summary>
        /// The key of a stored document or an embedded item with an id, or null. The key names
        /// <c>id</c> only when the property list carries it.
        /// </summary>
        public static IReadOnlyList<string>? KeyOf(Type type, IReadOnlyList<OxSchemaProperty> properties)
        {
            if (!IsKeyed(type))
                return null;

            var wire = WireNames.ConvertName(nameof(IItemId.Id));

            return properties.Any(property => property.Name == wire) ? [wire] : null;
        }

        private static bool IsKeyed(Type type) =>
            typeof(IItemId).IsAssignableFrom(type)
            || type.GetInterfaces().Any(candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IDocument<>));

        /// <summary>The first display candidate the type has as a string property, or null.</summary>
        public static string? DisplayOf(IReadOnlyList<OxSchemaProperty> properties)
        {
            foreach (var candidate in DisplayCandidates)
                if (properties.Any(property => property.Name == candidate && property.Kind == OxSchemaKinds.String))
                    return candidate;

            return null;
        }

        /// <summary>The label of an entity: its CLR name with a known suffix stripped, then de-PascalCased.</summary>
        public static string TypeLabel(Type type) => Humanize(StripLabelSuffix(type.Name));

        /// <summary>The label of a property, or null when it equals the one a consumer derives from the wire name.</summary>
        public static string? PropertyLabelOf(string clrName, string wireName)
        {
            var label = Humanize(clrName);

            return label == Humanize(Pascalize(wireName)) ? null : label;
        }

        /// <summary>The storage name of a property, or null when it equals the wire name with its first letter upper-cased.</summary>
        public static string? StorageNameOf(string clrName, string wireName) =>
            clrName == Pascalize(wireName) ? null : clrName;

        /// <summary>Upper-cases the first character and changes nothing else.</summary>
        private static string Pascalize(string wireName) =>
            string.IsNullOrEmpty(wireName) ? wireName : char.ToUpperInvariant(wireName[0]) + wireName[1..];

        private static string StripLabelSuffix(string name)
        {
            foreach (var suffix in LabelSuffixes)
                if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                    return name[..^suffix.Length];

            return name;
        }

        /// <summary>Splits a PascalCase name into words; an acronym run stays together, so <c>QRCode</c> becomes "QR Code".</summary>
        private static string Humanize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var label = new StringBuilder(name.Length + 8);

            for (var index = 0; index < name.Length; index++)
            {
                var character = name[index];

                var startsWord = index > 0
                    && char.IsUpper(character)
                    && (!char.IsUpper(name[index - 1]) || (index + 1 < name.Length && char.IsLower(name[index + 1])));

                if (startsWord)
                    label.Append(' ');

                label.Append(character);
            }

            return label.ToString();
        }
    }
}
