using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simplic.OxS;
using Simplic.OxS.GrpcSample.Client.Services;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.GrpcSample.Client;

/// <summary>
/// Main console application for demonstrating gRPC client using IRemoteServiceInvoker
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Simplic.OxS gRPC Sample Client ===");
        Console.WriteLine("This client demonstrates calling gRPC services using IRemoteServiceInvoker");
        Console.WriteLine();

        // Create host builder and configure services
        var host = CreateHostBuilder(args).Build();

        try
        {
            // Run the client application
            await RunClientAsync(host.Services);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running client: {ex.Message}");
        }
        finally
        {
            await host.StopAsync();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Mock request context for demonstration
                services.AddScoped<IRequestContext, MockRequestContext>();

                // Add remote service system - this registers IRemoteServiceInvoker
                services.AddRemoteService();

                // Add required services for IRemoteServiceInvoker
                services.AddMemoryCache();
                services.AddScoped<IDistributedCache, MockDistributedCache>();
                services.AddScoped<IEndpointContractRepository, MockEndpointContractRepository>();

                // Add our client service
                services.AddScoped<UserClientService>();
            });

    static async Task RunClientAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserClientService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Starting gRPC client demonstration...");

            // Test 1: List existing users
            Console.WriteLine("1. Listing existing users:");
            var users = await userService.ListUsersAsync();
            if (users != null)
            {
                Console.WriteLine($"Found {users.TotalCount} users total:");
                foreach (var user in users.Users)
                {
                    Console.WriteLine($"  - {user.Name} ({user.UserId}): {user.Email}, Age: {user.Age}, Active: {user.IsActive}");
                }
            }
            Console.WriteLine();

            // Test 2: Get a specific user
            Console.WriteLine("2. Getting specific user (user-1):");
            var specificUser = await userService.GetUserAsync("user-1");
            if (specificUser != null)
            {
                Console.WriteLine($"User found: {specificUser.Name} - {specificUser.Email}");
            }
            Console.WriteLine();

            // Test 3: Create a new user
            Console.WriteLine("3. Creating a new user:");
            var newUser = await userService.CreateUserAsync("Alice Cooper", "alice.cooper@rock.com", 28);
            if (newUser != null)
            {
                Console.WriteLine($"User created: {newUser.UserId} - {newUser.Message}");
            }
            Console.WriteLine();

            // Test 4: List users again to see the new user
            Console.WriteLine("4. Listing users again after creation:");
            var updatedUsers = await userService.ListUsersAsync();
            if (updatedUsers != null)
            {
                Console.WriteLine($"Found {updatedUsers.TotalCount} users total:");
                foreach (var user in updatedUsers.Users)
                {
                    Console.WriteLine($"  - {user.Name} ({user.UserId}): {user.Email}, Age: {user.Age}, Active: {user.IsActive}");
                }
            }
            Console.WriteLine();

            // Test 5: Try to get a non-existent user
            Console.WriteLine("5. Trying to get non-existent user:");
            try
            {
                await userService.GetUserAsync("non-existent-user");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Expected error: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Client demonstration completed successfully! ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during client demonstration");
            throw;
        }
    }
}

/// <summary>
/// Mock implementation of IRequestContext for demonstration purposes
/// </summary>
public class MockRequestContext : IRequestContext
{
    public Guid? OrganizationId { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public Guid? CorrelationId { get; set; } = Guid.NewGuid();
    public IDictionary<string, string> OxSHeaders { get; set; } = new Dictionary<string, string>();
}