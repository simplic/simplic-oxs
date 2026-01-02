using Microsoft.Extensions.Logging;
using Simplic.OxS;
using Simplic.OxS.GrpcSample.Client.Protos;

namespace Simplic.OxS.GrpcSample.Client.Services;
/// <summary>
/// <summary>
/// Service for calling the remote UserService using IRemoteServiceInvoker
/// </summary>
public class UserClientService
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker;
    private readonly ILogger<UserClientService> _logger;

    public UserClientService(IRemoteServiceInvoker remoteServiceInvoker, ILogger<UserClientService> logger)
    {
        _remoteServiceInvoker = remoteServiceInvoker;
        _logger = logger;
    }

    /// <summary>
    /// Gets user information by ID using remote service call
    /// </summary>
    public async Task<GetUserResponse?> GetUserAsync(string userId)
    {
        _logger.LogInformation("Calling remote GetUser service for user ID: {UserId}", userId);

        try
        {
            var request = new GetUserRequest { UserId = userId };

            // Call the remote service using the contract name or direct URI
            var response = await _remoteServiceInvoker.Call<GetUserResponse, GetUserRequest>(
                "[grpc]http://localhost:8082::user.UserService::GetUser",
                null,
                request,
                defaultImpl: async (req) =>
                {
                    _logger.LogWarning("Using default implementation for GetUser - remote service unavailable");
                    return new GetUserResponse
                    {
                        UserId = req.UserId,
                        Name = "Default User",
                        Email = "default@example.com",
                        Age = 0,
                        IsActive = false
                    };
                });

            _logger.LogInformation("Successfully received user: {UserName}", response?.Name);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling remote GetUser service");
            throw;
        }
    }

    /// <summary>
    /// Creates a new user using remote service call
    /// </summary>
    public async Task<CreateUserResponse?> CreateUserAsync(string name, string email, int age)
    {
        _logger.LogInformation("Calling remote CreateUser service for user: {UserName}", name);

        try
        {
            var request = new CreateUserRequest
            {
                Name = name,
                Email = email,
                Age = age
            };

            var response = await _remoteServiceInvoker.Call<CreateUserResponse, CreateUserRequest>(
                "[grpc]https://localhost:8082::user.UserService::CreateUser",
                null,
                request,
                defaultImpl: async (req) =>
                {
                    _logger.LogWarning("Using default implementation for CreateUser - remote service unavailable");
                    return new CreateUserResponse
                    {
                        UserId = $"default-{Guid.NewGuid():N}",
                        Success = true,
                        Message = "Created using default implementation"
                    };
                });

            _logger.LogInformation("Successfully created user: {UserId}", response?.UserId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling remote CreateUser service");
            throw;
        }
    }

    /// <summary>
    /// Lists all users using remote service call
    /// </summary>
    public async Task<ListUsersResponse?> ListUsersAsync(int pageSize = 10, int pageNumber = 1)
    {
        _logger.LogInformation("Calling remote ListUsers service with page size: {PageSize}, page: {PageNumber}",
            pageSize, pageNumber);

        try
        {
            var request = new ListUsersRequest
            {
                PageSize = pageSize,
                PageNumber = pageNumber
            };

            var response = await _remoteServiceInvoker.Call<ListUsersResponse, ListUsersRequest>(
                "[grpc]https://localhost:8082::user.UserService::ListUsers",
                null,
                request,                
                defaultImpl: async (req) =>
                {
                    _logger.LogWarning("Using default implementation for ListUsers - remote service unavailable");
                    var defaultResponse = new ListUsersResponse { TotalCount = 0 };
                    defaultResponse.Users.Add(new GetUserResponse
                    {
                        UserId = "default-1",
                        Name = "Default User",
                        Email = "default@example.com",
                        Age = 0,
                        IsActive = false
                    });
                    return defaultResponse;
                });

            _logger.LogInformation("Successfully received {UserCount} users out of {TotalCount}",
                response?.Users.Count, response?.TotalCount);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling remote ListUsers service");
            throw;
        }
    }
}