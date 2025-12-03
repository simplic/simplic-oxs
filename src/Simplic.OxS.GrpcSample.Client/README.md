# Simplic.OxS gRPC Sample Client

This is a sample console client application that demonstrates how to use `IRemoteServiceInvoker` to call gRPC services in the Simplic.OxS ecosystem.

## Features

- Demonstrates usage of `IRemoteServiceInvoker` for gRPC calls
- Includes fallback/default implementations
- Proper dependency injection setup
- Mock implementations for testing
- Comprehensive logging and error handling

## What it demonstrates

The client shows how to:
1. Configure `IRemoteServiceInvoker` in a console application
2. Make gRPC calls using contract-based routing
3. Implement fallback logic when services are unavailable
4. Handle errors and logging properly
5. Use generated protobuf classes for type-safe communication

## Running the Client

1. **Start the server first:**
   ```bash
   cd ../Simplic.OxS.GrpcSample.Server
   dotnet run
   ```

2. **Build and run the client:**
   ```bash
   dotnet build
   dotnet run
   ```

## Sample Output

```
=== Simplic.OxS gRPC Sample Client ===
This client demonstrates calling gRPC services using IRemoteServiceInvoker

1. Listing existing users:
Found 3 users total:
  - John Doe (user-1): john.doe@example.com, Age: 30, Active: True
  - Jane Smith (user-2): jane.smith@example.com, Age: 25, Active: True
  - Bob Johnson (user-3): bob.johnson@example.com, Age: 35, Active: False

2. Getting specific user (user-1):
User found: John Doe - john.doe@example.com

3. Creating a new user:
User created: user-4 - User created successfully

4. Listing users again after creation:
Found 4 users total:
  - John Doe (user-1): john.doe@example.com, Age: 30, Active: True
  - Jane Smith (user-2): jane.smith@example.com, Age: 25, Active: True
  - Bob Johnson (user-3): bob.johnson@example.com, Age: 35, Active: False
  - Alice Cooper (user-4): alice.cooper@rock.com, Age: 28, Active: True

5. Trying to get non-existent user:
Expected error: Status(StatusCode="NotFound", Detail="User with ID 'non-existent-user' not found.")

=== Client demonstration completed successfully! ===
```

## Project Structure

```
Simplic.OxS.GrpcSample.Client/
??? Protos/
?   ??? user.proto              # Protobuf client definitions
??? Services/
?   ??? UserClientService.cs    # Service wrapper for remote calls
??? MockImplementations.cs      # Mock services for demonstration
??? Program.cs                  # Main application logic
??? appsettings.json           # Configuration
??? *.csproj                   # Project file
```

## Key Implementation Details

### Remote Service Calls

The `UserClientService.cs` demonstrates:
- Using `IRemoteServiceInvoker.Call<TResponse, TRequest>()` 
- Direct URI specification: `[grpc]https://localhost:8443::user.UserService::GetUser`
- Fallback implementations for service unavailability
- Proper error handling and logging

### URI Format

The gRPC URI format used is:
```
[grpc]<server-address>::<service-name>::<method-name>
```

Examples:
- `[grpc]https://localhost:8443::user.UserService::GetUser`
- `[grpc]https://localhost:8443::user.UserService::CreateUser`

### Mock Implementations

For demonstration purposes, the client includes:
- `MockRequestContext`: Provides organization and user IDs
- `MockDistributedCache`: Simple in-memory cache implementation
- `MockEndpointContractRepository`: Returns empty results (forces direct URI usage)

### Dependency Injection Setup

The `Program.cs` shows how to:
- Register `IRemoteServiceInvoker` using `AddRemoteService()`
- Configure required dependencies
- Set up proper logging
- Create service scopes for testing

## Configuration

The client uses minimal configuration in `appsettings.json`:
- Logging configuration for different components
- Debug logging for gRPC client operations

## Error Handling

The client demonstrates:
- Catching and handling gRPC exceptions
- Using fallback implementations when services are down
- Proper logging of errors and successful operations

## Testing Scenarios

The client tests the following scenarios:
1. **List Users**: Retrieve all users with pagination
2. **Get User**: Retrieve a specific user by ID
3. **Create User**: Add a new user to the system
4. **Verify Creation**: List users again to confirm creation
5. **Error Handling**: Attempt to get a non-existent user

## Dependencies

- Simplic.OxS - Core interfaces (`IRemoteServiceInvoker`)
- Simplic.OxS.Server - Extensions and implementations
- Google.Protobuf - Protocol buffers support
- Grpc.Net.Client - gRPC client libraries
- Microsoft.Extensions.Hosting - Console application hosting