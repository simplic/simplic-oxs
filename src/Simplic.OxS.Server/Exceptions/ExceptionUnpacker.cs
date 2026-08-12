using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Walks an exception chain, unwrapping every exception annotated with
/// <see cref="UnpackExceptionAttribute"/> so a targeted inner exception can be reached.
/// <para>
/// This is the shared successor of the unwrap logic that previously lived on
/// <c>CommonExceptionFilterAttribute</c>; it is used by the global
/// <c>IExceptionHandler</c> chain.
/// </para>
/// </summary>
public static class ExceptionUnpacker
{
    /// <summary>
    /// Attempts to find an exception of type <typeparamref name="TException"/> within
    /// <paramref name="exception"/> or its unpackable inner-exception chain.
    /// </summary>
    /// <typeparam name="TException">The exception type to look for.</typeparam>
    /// <param name="exception">The thrown exception.</param>
    /// <param name="target">The matched exception when found.</param>
    /// <returns><see langword="true"/> when a matching exception was found.</returns>
    public static bool TryUnpack<TException>(Exception exception, [NotNullWhen(true)] out TException? target)
        where TException : Exception
    {
        for (var current = exception; current != null; current = Unpack(current))
        {
            if (current is TException match)
            {
                target = match;
                return true;
            }
        }

        target = null;
        return false;
    }

    /// <summary>
    /// Returns the inner exception when <paramref name="exception"/> is annotated with
    /// <see cref="UnpackExceptionAttribute"/>; otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="exception">The exception to unpack.</param>
    /// <returns>The inner exception, or <see langword="null"/> when the chain should stop.</returns>
    public static Exception? Unpack(Exception exception)
    {
        if (exception.GetType().GetCustomAttribute<UnpackExceptionAttribute>() == null)
            return null;

        return exception.InnerException;
    }
}
