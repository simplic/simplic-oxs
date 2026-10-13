using MongoDB.Bson;

namespace Simplic.OxS.Server
{
    /// <summary>
    /// Says what to do about the MongoDB driver's refusal to register a serializer for a type it
    /// already has one for, where a service's own registrations meet it while the host starts.
    /// <para>
    /// The driver creates the default serializer of a type the first time anything looks it up:
    /// a class map that asks for a member's serializer, a serializer that keeps another one
    /// (one for an interface or a discriminated type keeps the object serializer), a lookup in
    /// the service's code. From then on <c>BsonSerializer.RegisterSerializer</c> for that type
    /// throws <c>There is already a serializer registered for type …</c>, and
    /// <c>TryRegisterSerializer</c> throws <c>There is already a different serializer registered
    /// for type …</c> for a serializer that is not the one in place (and returns false for one
    /// that is). Neither message names the earlier lookup or the remedy. The remedy is an
    /// order: plain serializers first.
    /// </para>
    /// </summary>
    internal static class SerializerRegistrationOrder
    {
        /// <summary>What the driver's messages start with: <c>RegisterSerializer</c>'s and <c>TryRegisterSerializer</c>'s.</summary>
        internal static readonly string[] DriverMessages =
        [
            "There is already a serializer registered for type",
            "There is already a different serializer registered for type",
        ];

        /// <summary>What the explained exception says after the driver's own sentence.</summary>
        internal const string Remedy =
            "The type had a serializer before this registration: one registered earlier, or the default the driver creates when a registration that ran earlier first looked the type's serializer up, "
            + "as a class map that asks for a member's serializer and a serializer that keeps another one do. "
            + "Register this serializer at the top of RegisterBsonClassMaps(), before anything that constructs a serializer or a class map. "
            + "BsonSerializer.TryRegisterSerializer does not help: the serializer in place stays, and the registration that looked it up keeps it.";

        /// <summary>
        /// Runs <paramref name="registrations"/>, the service's code of <paramref name="place"/>.
        /// The driver's refusal, thrown there or as the cause of what is thrown there, is thrown
        /// as an exception of the driver's type whose message adds where it was met and the
        /// remedy, with what was thrown as its inner exception. Everything else passes as it is.
        /// </summary>
        /// <param name="place">The member of the startup that ran, e.g. <c>RegisterServices</c>.</param>
        /// <param name="registrations">The service's code.</param>
        internal static void Explain(string place, Action registrations)
        {
            try
            {
                registrations();
            }
            catch (Exception exception) when (ConflictOf(exception) is { } conflict)
            {
                throw new BsonSerializationException($"{conflict.Message.TrimEnd()} Thrown in {place}. {Remedy}", exception);
            }
        }

        /// <inheritdoc cref="Explain(string, Action)"/>
        internal static T Explain<T>(string place, Func<T> registrations)
        {
            T result = default!;

            Explain(place, () => { result = registrations(); });

            return result;
        }

        /// <summary>
        /// The driver's refusal in <paramref name="exception"/> or among its causes, unless it is
        /// explained already (a hook that threw is thrown again as it was).
        /// </summary>
        private static BsonSerializationException? ConflictOf(Exception exception)
        {
            for (var cause = exception; cause is not null; cause = cause.InnerException)
            {
                if (cause is BsonSerializationException refusal && DriverMessages.Any(message => refusal.Message.StartsWith(message, StringComparison.Ordinal)))
                    return refusal.Message.Contains(Remedy, StringComparison.Ordinal) ? null : refusal;
            }

            return null;
        }
    }
}
