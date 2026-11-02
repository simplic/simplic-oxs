using Microsoft.AspNetCore.Mvc;

namespace Simplic.OxS.Server;

public static class Results
{
    public static bool IsValidResourceTypeName(string resourceTypeName)
    {
        return !string.IsNullOrWhiteSpace(resourceTypeName) && !resourceTypeName.Contains('@');
    }

    public static string NotFoundIdentifier(string resourceTypeName, object? id)
    {
        if (!IsValidResourceTypeName(resourceTypeName))
            throw new ArgumentException("Invalid resource type name.", nameof(resourceTypeName));

        if (id == null)
            return resourceTypeName;
        else
            return $"{resourceTypeName}@{id}";
    }

    public static IActionResult NotFound(string resourceTypeName, object? id)
    {
        return new NotFoundObjectResult(NotFoundIdentifier(resourceTypeName, id));
    }
}
