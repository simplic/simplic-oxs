using Grpc.Core;
using Microsoft.Extensions.Logging;
using Simplic.OxS;
using Simplic.OxS.GrpcSample.Server.Protos;

namespace Simplic.OxS.GrpcSample.Server.Services;

/// <summary>
/// User service implementation for gRPC
/// </summary>
public class UserService : Protos.UserService.UserServiceBase
{
    private readonly ILogger<UserService> _logger;
    private readonly IRequestContext _requestContext;
    
    // Sample in-memory data store
    private static readonly Dictionary<string, GetUserResponse> Users = new()
    {
        ["user-1"] = new() { UserId = "user-1", Name = "John Doe", Email = "john.doe@example.com", Age = 30, IsActive = true },
        ["user-2"] = new() { UserId = "user-2", Name = "Jane Smith", Email = "jane.smith@example.com", Age = 25, IsActive = true },
        ["user-3"] = new() { UserId = "user-3", Name = "Bob Johnson", Email = "bob.johnson@example.com", Age = 35, IsActive = false }
    };

    public UserService(ILogger<UserService> logger, IRequestContext requestContext)
    {
        _logger = logger;
        _requestContext = requestContext;
    }

    /// <summary>
    /// Gets user information by ID
    /// </summary>
    public override Task<GetUserResponse> GetUser(GetUserRequest request, ServerCallContext context)
    {
        _logger.LogInformation("GetUser called for user ID: {UserId} by organization: {OrganizationId}, user: {UserId}",
            request.UserId, _requestContext.OrganizationId, _requestContext.UserId);

        if (Users.TryGetValue(request.UserId, out var user))
        {
            _logger.LogInformation("User found: {UserName}", user.Name);
            return Task.FromResult(user);
        }

        _logger.LogWarning("User not found: {UserId}", request.UserId);
        throw new RpcException(new Status(StatusCode.NotFound, $"User with ID '{request.UserId}' not found."));
    }

    /// <summary>
    /// Creates a new user
    /// </summary>
    public override Task<CreateUserResponse> CreateUser(CreateUserRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateUser called for: {UserName} by organization: {OrganizationId}, user: {UserId}",
            request.Name, _requestContext.OrganizationId, _requestContext.UserId);

        var userId = $"user-{Users.Count + 1}";
        var newUser = new GetUserResponse
        {
            UserId = userId,
            Name = request.Name,
            Email = request.Email,
            Age = request.Age,
            IsActive = true
        };

        Users[userId] = newUser;

        _logger.LogInformation("User created successfully: {UserId}", userId);

        return Task.FromResult(new CreateUserResponse
        {
            UserId = userId,
            Success = true,
            Message = "User created successfully"
        });
    }

    /// <summary>
    /// Lists all users with pagination
    /// </summary>
    public override Task<ListUsersResponse> ListUsers(ListUsersRequest request, ServerCallContext context)
    {
        _logger.LogInformation("ListUsers called with page size: {PageSize}, page: {PageNumber} by organization: {OrganizationId}, user: {UserId}",
            request.PageSize, request.PageNumber, _requestContext.OrganizationId, _requestContext.UserId);

        var pageSize = request.PageSize > 0 ? request.PageSize : 10;
        var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
        var skip = (pageNumber - 1) * pageSize;

        var users = Users.Values
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        var response = new ListUsersResponse
        {
            TotalCount = Users.Count
        };
        
        response.Users.AddRange(users);

        _logger.LogInformation("Returning {UserCount} users out of {TotalUsers}",
            users.Count, Users.Count);

        return Task.FromResult(response);
    }
}