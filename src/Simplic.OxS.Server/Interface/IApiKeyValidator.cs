namespace Simplic.OxS.Server.Interface;

public interface IApiKeyValidator
{
    bool TryValidateApiKey(string apiKey, out Guid? userId, out Guid? organizationId);
}
