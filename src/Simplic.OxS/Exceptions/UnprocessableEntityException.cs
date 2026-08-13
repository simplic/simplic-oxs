namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>422 Unprocessable Content</c> response.
/// Throw when the request is syntactically valid but violates semantic (business) validation rules.
/// <para>
/// Can carry field-level validation errors, emitted under the <c>errors</c> member of the problem
/// details, mirroring the shape of ASP.NET Core's <c>ValidationProblemDetails</c> (a map of property
/// path to one or more problem messages).
/// </para>
/// </summary>
public class UnprocessableEntityException : OxSException
{
    private readonly Dictionary<string, List<string>> errors = new();

    /// <summary>
    /// Initializes a new <see cref="UnprocessableEntityException"/> with a plain message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public UnprocessableEntityException(string? message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="UnprocessableEntityException"/> for a single failed field.
    /// </summary>
    /// <param name="propertyPath">The path of the property that failed (e.g. <c>customer.name</c>).</param>
    /// <param name="problem">A human-readable description of why the field is invalid.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public UnprocessableEntityException(string propertyPath, string problem, Exception? innerException = null)
        : base($"Validation failed for '{propertyPath}': {problem}", innerException)
    {
        AddError(propertyPath, problem);
    }

    /// <summary>
    /// Initializes a new <see cref="UnprocessableEntityException"/> from a set of field errors.
    /// </summary>
    /// <param name="errors">A map of property path to one or more problem messages.</param>
    /// <param name="message">
    /// Optional detail message. Defaults to <c>"One or more validation errors occurred."</c>.
    /// </param>
    /// <param name="innerException">Optional inner exception.</param>
    public UnprocessableEntityException(
        IReadOnlyDictionary<string, string[]> errors,
        string? message = null,
        Exception? innerException = null)
        : base(message ?? "One or more validation errors occurred.", innerException)
    {
        foreach (var (propertyPath, problems) in errors)
        {
            foreach (var problem in problems)
                AddError(propertyPath, problem);
        }
    }

    /// <inheritdoc/>
    public override int StatusCode => 422;

    /// <inheritdoc/>
    public override string? Title => "Unprocessable Content";

    /// <inheritdoc/>
    public override string? ProblemType => "urn:simplic-oxs:problem:unprocessable-entity";

    /// <summary>
    /// Adds a field-level validation error. Multiple problems can be added for the same property path.
    /// </summary>
    /// <param name="propertyPath">The path of the property that failed (e.g. <c>customer.name</c>).</param>
    /// <param name="problem">A human-readable description of why the field is invalid.</param>
    /// <returns>The same instance, to allow fluent chaining.</returns>
    public UnprocessableEntityException AddError(string propertyPath, string problem)
    {
        if (!errors.TryGetValue(propertyPath, out var problems))
        {
            problems = new List<string>();
            errors[propertyPath] = problems;
        }

        problems.Add(problem);
        return this;
    }

    /// <inheritdoc/>
    public override void PopulateProblemDetails(IDictionary<string, object?> extensions)
    {
        if (errors.Count > 0)
            extensions["errors"] = errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }
}
