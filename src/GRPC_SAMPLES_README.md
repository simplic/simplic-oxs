# Simplic.OxS gRPC Sample Applications

This folder contains two sample applications that demonstrate how to build and consume gRPC services using the Simplic.OxS framework:

1. **Simplic.OxS.GrpcSample.Server** - A gRPC server using Bootstrap.cs
2. **Simplic.OxS.GrpcSample.Client** - A console client using IRemoteServiceInvoker

## Overview

These samples show the complete lifecycle of building microservices with the Simplic.OxS ecosystem:

- **Server**: How to create a gRPC service integrated with OxS monitoring, logging, authentication, and middleware
- **Client**: How to consume gRPC services using the built-in service discovery and remote calling mechanisms

## Architecture

```
???????????????????    gRPC over HTTPS    ???????????????????
?                 ?    (Port 5001)        ?                 ?
?  Sample Client  ? ????????????????????? ?  Sample Server  ?
?                 ?                       ?                 ?
? IRemoteService  ?                       ?   Bootstrap.cs  ?
? Invoker         ?                       ?   + UserService ?
???????????????????                       ???????????????????
```

## Quick Start

1. **Start the server:**
   ```bash
   cd Simplic.OxS.GrpcSample.Server
   dotnet run
   ```

2. **Run the client (in a new terminal):**
   ```bash
   cd Simplic.OxS.GrpcSample.Client
   dotnet run
   ```

## Sample Service: UserService

Both applications implement a sample user management service with these operations:

| Method | Description | Input | Output |
|--------|-------------|--------|--------|
| `GetUser` | Retrieve user by ID | `GetUserRequest` | `GetUserResponse` |
| `CreateUser` | Create a new user | `CreateUserRequest` | `CreateUserResponse` |
| `ListUsers` | List all users with pagination | `ListUsersRequest` | `ListUsersResponse` |

## Key Features Demonstrated

### Server Side (Simplic.OxS.GrpcSample.Server)
- ? Bootstrap.cs integration
- ? gRPC service implementation
- ? Protobuf code generation
- ? Request context injection
- ? Structured logging
- ? Error handling with gRPC status codes
- ? OpenTelemetry monitoring integration
- ? Middleware pipeline integration

### Client Side (Simplic.OxS.GrpcSample.Client)  
- ? IRemoteServiceInvoker usage
- ? Type-safe gRPC calls
- ? Fallback/default implementations
- ? Dependency injection setup
- ? Error handling and retry logic
- ? Structured logging

## Technologies Used

- **gRPC & Protocol Buffers**: Service contracts and code generation
- **ASP.NET Core**: Web hosting and middleware
- **Simplic.OxS Framework**: Bootstrap, middleware, monitoring, service discovery
- **OpenTelemetry**: Distributed tracing and metrics
- **Dependency Injection**: Service registration and scoping
- **.NET 10**: Latest runtime features

## Production Considerations

These samples include patterns for production use:

### Security
- HTTPS/TLS for gRPC communication
- Request context with organization/user IDs
- Host validation middleware

### Observability  
- Structured logging with correlation IDs
- OpenTelemetry tracing and metrics
- Console and OTLP exporters

### Resilience
- Fallback implementations for service unavailability
- Proper error handling and status codes
- Service discovery with caching

### Performance
- Efficient protobuf serialization
- Connection pooling (built into gRPC client)
- Caching of service endpoints

## Extending the Samples

You can extend these samples by:

1. **Adding more service methods** to the protobuf definition
2. **Implementing authentication** using JWT tokens
3. **Adding database persistence** instead of in-memory storage
4. **Implementing service discovery** with contract-based routing
5. **Adding unit tests** for both client and server components
6. **Configuring OTLP exporters** to send data to observability platforms

## Related Documentation

- [Simplic.OxS.Server Documentation](../Simplic.OxS.Server/README.md)
- [gRPC for .NET Documentation](https://docs.microsoft.com/en-us/aspnet/core/grpc/)
- [Protocol Buffers Guide](https://developers.google.com/protocol-buffers)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)

## Troubleshooting

### Server won't start
- Check if ports 5000/5001 are available
- Verify certificate configuration for HTTPS

### Client can't connect
- Ensure server is running first
- Check firewall settings for localhost connections
- Verify gRPC endpoint URL in client configuration

### Build errors
- Make sure all project dependencies are restored: `dotnet restore`
- Check that protobuf files are correctly configured in project files