namespace Simplic.OxS.Server.Interface;

public interface IApiKeyValidator
{
    Task<bool> TryValidateApiKeyAsync(string apiKey, out Guid? userId, out Guid? organizationId);
}
