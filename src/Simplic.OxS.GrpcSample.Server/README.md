# Simplic.OxS gRPC Sample Server

This is a sample server application that demonstrates how to use the Simplic.OxS.Server framework with Bootstrap.cs to create a gRPC service.

## Features

- Built on top of Simplic.OxS.Server Bootstrap framework
- Implements a sample UserService with gRPC endpoints
- Demonstrates proper integration with OxS monitoring, logging, and middleware
- Uses protobuf for service definitions
- Includes proper request context handling

## Service Endpoints

The server provides the following gRPC endpoints:

### UserService
- **GetUser**: Retrieves user information by ID
- **CreateUser**: Creates a new user
- **ListUsers**: Lists all users with pagination

## Running the Server

1. **Build the project:**
   ```bash
   dotnet build
   ```

2. **Run the server:**
   ```bash
   dotnet run
   ```

3. **Server endpoints:**
   - HTTP: `http://localhost:8080`
   - HTTPS/gRPC: `http://localhost:8082`
   - Swagger UI: `http://localhost:8082/grpcsample-api/v1/swagger`

## Configuration

The server uses standard ASP.NET Core configuration:
- `appsettings.json` - Main configuration
- `appsettings.Development.json` - Development overrides

Key configuration sections:
- **Monitoring**: OpenTelemetry tracing, metrics, and logging
- **Kestrel**: HTTP/HTTPS endpoint configuration
- **Logging**: Log level configuration

## Project Structure

```
Simplic.OxS.GrpcSample.Server/
??? Protos/
?   ??? user.proto              # Protobuf service definition
??? Services/
?   ??? UserService.cs          # gRPC service implementation
??? Startup.cs                  # Bootstrap configuration
??? Program.cs                  # Application entry point
??? appsettings.json           # Configuration
??? *.csproj                   # Project file
```

## Key Implementation Details

### Bootstrap Integration

The `Startup.cs` class inherits from `Bootstrap` and demonstrates:
- Service registration in `RegisterServices()`
- gRPC endpoint mapping in `MapGrpcEndpoints()`
- Configuration overrides in `Configure()`

### Service Implementation

The `UserService.cs` demonstrates:
- Proper gRPC service inheritance from generated base class
- Dependency injection of `ILogger` and `IRequestContext`
- Error handling with gRPC status codes
- Logging with organization and user context

### Protobuf Definition

The `user.proto` file defines:
- Service methods and their input/output types
- Message structures with proper C# namespace generation
- Standard gRPC patterns for CRUD operations

## Testing

You can test the gRPC service using:
1. The provided client application (`Simplic.OxS.GrpcSample.Client`)
2. gRPC clients like BloomRPC or Postman
3. Custom client applications using the generated protobuf classes

## Dependencies

- Simplic.OxS.Server - Core server framework
- Google.Protobuf - Protocol buffers support
- Grpc.AspNetCore - ASP.NET Core gRPC integration