# gRPC Proto File Runtime Loading Feature

## Overview

This feature automatically reads proto files at runtime and creates instances of `GrpcDefinitions` to add to the `ServiceObject.GrpcDefinitions` collection. This enables automatic discovery and exposure of gRPC service definitions without manual configuration.

## How it Works

The `ServiceDefinitionService` now includes functionality to:
1. Scan the application base directory for `.proto` files
2. Parse proto file content to extract package and service information
3. Create `GrpcDefinitions` instances with the proto file content
4. Add these definitions to the `ServiceObject.GrpcDefinitions` collection

## Implementation Details

### ServiceDefinitionService Changes

The `Fill()` method now calls `LoadGrpcDefinitions()` which:
- Searches for `*.proto` files recursively in the application base directory
- Processes each proto file to extract:
  - Package name (from `package` statement)
  - Service name(s) (from `service` declarations)
  - Raw proto file content as byte array

### Proto File Parsing

The parsing uses regular expressions to extract:
- **Package**: `package\s+([^;]+);` - extracts package name
- **Service**: `service\s+(\w+)\s*\{` - extracts service names

For services, the full service name combines package and service: `{package}.{service}`

### Error Handling

The implementation includes comprehensive error handling:
- File system errors (directory not accessible, files not readable)
- Proto parsing errors (malformed proto files)
- Missing services (proto files without service definitions)
- Continues processing other files if one fails

## Usage Example

### gRPC Sample Server Integration

The gRPC sample server now includes a `ServiceDefinitionController` that exposes:

1. **Service Definition Endpoint** (`GET /api/servicedefinition`):
   ```json
   {
     "serviceName": "GrpcSample",
     "version": "v1", 
     "type": "internal",
     "baseUrl": "/GrpcSample-api/v1/",
     "grpcDefinitions": [
       {
         "package": "user",
         "service": "user.UserService",
         "protoFileSize": 1234,
         "hasProtoFile": true
       }
     ]
   }
   ```

2. **Proto File Download** (`GET /api/servicedefinition/grpc/{serviceName}/proto`):
   Returns the raw proto file content for a specific service.

### Automatic Discovery

When a service starts up, the `ServiceDefinitionService` automatically:
1. Scans for proto files in the runtime directory
2. Parses and loads them into `GrpcDefinitions` collection
3. Logs the discovery process to console

Example console output:
```
Found 2 proto file(s) in C:\app
Processing proto file: C:\app\user.proto
Added gRPC definition - Package: user, Service: user.UserService
Processing proto file: C:\app\test.proto
Added gRPC definition - Package: test, Service: test.TestService
```

## Proto File Structure Support

The parser supports standard proto3 syntax:

```proto
syntax = "proto3";

option csharp_namespace = "MyApp.Protos";

package mypackage;

service MyService {
  rpc GetData (GetDataRequest) returns (GetDataResponse);
  rpc CreateData (CreateDataRequest) returns (CreateDataResponse);
}

message GetDataRequest {
  string id = 1;
}
```

## File Deployment

Proto files must be deployed alongside the application in any subdirectory of the application base directory. Common patterns:

- `{AppDir}/Protos/*.proto` 
- `{AppDir}/*.proto`
- `{AppDir}/Services/*/Protos/*.proto`

## Benefits

1. **Automatic Discovery**: No manual configuration required
2. **Runtime Availability**: Proto files available for service introspection
3. **Documentation**: Services can expose their proto definitions
4. **Tool Integration**: External tools can download proto files for client generation
5. **Service Registry**: Enables building service registries with gRPC definition metadata

## Testing

The feature includes comprehensive unit tests:
- `ServiceDefinitionServiceTest`: Basic service definition functionality
- `ServiceDefinitionServiceGrpcTest`: gRPC-specific functionality and edge cases

## Error Scenarios

The implementation gracefully handles:
- Missing proto files (empty collection)
- Malformed proto files (skipped with logging)
- Proto files without services (skipped with logging)  
- File system access errors (logged, continues with other files)
- Multiple services per proto file (uses first service found)

## Future Enhancements

Potential improvements could include:
- Support for multiple services per proto file (create separate GrpcDefinitions)
- Proto file validation beyond basic parsing
- Caching of parsed results to avoid re-parsing on every startup
- Support for proto file URLs/remote loading
- Integration with service discovery systems