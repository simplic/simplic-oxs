# PatchHelper - Comprehensive Documentation

The `PatchHelper` is a powerful utility class designed to apply partial updates (patches) to objects based on HTTP PATCH requests. It intelligently processes JSON input to determine which properties should be updated and provides extensive configuration options for custom behavior.

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Basic Usage](#basic-usage)
4. [Advanced Features](#advanced-features)
5. [Configuration System](#configuration-system)
6. [Collection Handling](#collection-handling)
7. [Validation System](#validation-system)
8. [Error Handling](#error-handling)
9. [Best Practices](#best-practices)
10. [API Reference](#api-reference)
11. [Examples](#examples)

## Overview

The `PatchHelper` enables you to:

- **Selective Updates**: Update only the properties specified in JSON input
- **Complex Object Support**: Handle nested objects and complex hierarchies
- **Collection Management**: Add, update, and remove items in collections intelligently
- **Validation**: Validate changes before they're applied with custom validation logic
- **Configuration**: Configure custom behaviors for specific properties or collections
- **Type Safety**: Maintain type safety while working with dynamic JSON input

### Key Benefits

- **JSON-Driven**: Only updates properties that are present in the JSON request
- **Flexible**: Supports simple properties, nested objects, and collections
- **Extensible**: Configurable behavior for custom business logic
- **Robust**: Comprehensive error handling and validation
- **Performance**: Efficient processing with minimal reflection overhead

## Architecture

The PatchHelper follows a recursive, queue-based processing model:

```
JSON Input ? Parse ? Queue Processing ? Property Updates ? Validation ? Result
```

### Core Components

1. **PatchHelper**: Main orchestrator class
2. **PatchConfiguration**: Configuration container for custom behaviors
3. **ValidationRequest**: Context object for validation decisions
4. **PatchConfigurationItem**: Custom property-level behavior
5. **PatchConfigurationCollectionItem**: Custom collection-level behavior

## Basic Usage

### Simple Property Updates

```csharp
var original = new Person { FirstName = "John", LastName = "Doe" };
var patch = new PersonRequest { LastName = "Smith" };
var json = @"{""LastName"": ""Smith""}";

var patchHelper = new PatchHelper();
var result = await patchHelper.Patch(original, patch, json, validation => true);

// Result: original.LastName is now "Smith", FirstName remains "John"
```

### Nested Object Updates

```csharp
var json = @"{
    ""Address"": {
        ""Street"": ""123 Main St"",
        ""City"": ""Springfield""
    }
}";

var result = await patchHelper.Patch(original, patch, json, validation => true);
```

### Case-Insensitive Property Matching

The PatchHelper automatically handles case-insensitive property names:

```csharp
// All these JSON formats work identically:
var json1 = @"{""firstName"": ""John""}";      // camelCase
var json2 = @"{""FirstName"": ""John""}";      // PascalCase  
var json3 = @"{""FIRSTNAME"": ""John""}";      // UPPERCASE
```

## Advanced Features

### Nullable Type Handling

Automatically handles conversion between nullable and non-nullable types:

```csharp
// Patch object has nullable properties, original has non-nullable
var patch = new PersonRequest { Age = 25 };           // int?
var original = new Person { Age = 0 };                // int

// PatchHelper handles the conversion automatically
```

### Uninitialized Property Initialization

Automatically initializes null nested objects:

```csharp
var original = new Person { Address = null };  // Address is null
var json = @"{""Address"": {""Street"": ""123 Main St""}}";

// PatchHelper creates new Address instance and sets Street property
var result = await patchHelper.Patch(original, patch, json, validation => true);
```

### Dictionary Support

Handles dictionary properties for dynamic key-value data:

```csharp
var json = @"{""AddonProperties"": {""CustomField1"": ""Value1""}}";
// Adds or updates dictionary entries based on JSON structure
```

## Configuration System

The configuration system allows you to define custom behaviors for specific properties or property patterns.

### Constructor Options

```csharp
// Basic constructor
var helper = new PatchHelper();

// With pre-configured configuration
var config = new PatchConfiguration();
var helper = new PatchHelper(config);

// With inline configuration
var helper = new PatchHelper(cfg => 
{
    cfg.ForPath("Email").ChangeAction<User, UserRequest>((original, patch) => 
    {
        original.Email = patch.Email.ToLower();
        original.EmailVerified = false;
        return Task.CompletedTask;
    });
    return cfg;
});
```

### Property-Level Configuration

Define custom behavior for specific properties:

```csharp
var helper = new PatchHelper(cfg =>
{
    cfg.ForPath("Email").ChangeAction<User, UserRequest>((original, patch) =>
    {
        // Custom email processing
        original.Email = patch.Email.ToLower();
        original.EmailUpdatedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    });
    
    return cfg;
});
```

### Partial Path Matching

Use start and end patterns for flexible path matching:

```csharp
cfg.ForPath("Items", "TypeId").ChangeAction<TransactionItem, TransactionItemRequest>((original, patch) =>
{
    // Matches any path that starts with "Items" and ends with "TypeId"
    // Examples: "Items.TypeId", "Items.SubItems.TypeId", "Items[0].TypeId"
    original.Type = new ItemType { Id = patch.TypeId.Value };
    return Task.CompletedTask;
});
```

## Collection Handling

The PatchHelper provides sophisticated collection management with support for add, update, and delete operations.

### Collection Item Identification

Items in collections are identified by their `Id` property (must implement `IItemId`):

```csharp
public class PhoneNumber : IItemId
{
    public Guid Id { get; set; }
    public string Number { get; set; }
}
```

### Adding New Items

Items without an `Id` or with `Id = Guid.Empty` are treated as new:

```csharp
var json = @"{
    ""PhoneNumbers"": [
        {""Number"": ""123-456-7890""}  // No Id = new item
    ]
}";
```

### Updating Existing Items

Items with existing IDs are updated:

```csharp
var json = @"{
    ""PhoneNumbers"": [
        {""Id"": ""existing-guid-here"", ""Number"": ""updated-number""}
    ]
}";
```

### Removing Items

Items with `_remove: true` are deleted from the collection:

```csharp
var json = @"{
    ""PhoneNumbers"": [
        {""Id"": ""existing-guid-here"", ""_remove"": true}
    ]
}";
```

### Simple Type Collections

For collections of simple types (strings, numbers, etc.), the entire collection is replaced:

```csharp
var json = @"{""Tags"": [""tag1"", ""tag2"", ""tag3""]}";
// Replaces the entire Tags collection with these three values
```

### Empty Collections

- **Complex Objects**: Empty arrays are ignored for complex object collections
- **Simple Types**: Empty arrays clear the collection for simple type collections

```csharp
// Complex objects - ignored (preserves existing items)
var json1 = @"{""PhoneNumbers"": []}";

// Simple types - clears collection  
var json2 = @"{""Tags"": []}";
```

### Custom Collection Behavior

#### Custom Item Creation

```csharp
cfg.ForCollectionPath("PhoneNumbers").ChangeAddItem<PhoneNumberRequest, PhoneNumber>(patch =>
{
    return new PhoneNumber 
    { 
        Id = Guid.NewGuid(),
        Number = patch.PhoneNumber,
        CreatedAt = DateTime.UtcNow
    };
});
```

#### Collection Replacement

```csharp
cfg.ForCollectionPath("Tags").OverwriteCollectionInPatch<List<string>, List<string>>(patch =>
{
    return patch.Select(tag => tag.ToUpper()).ToList();
});
```

#### Nested Collection Configuration

```csharp
cfg.ForCollectionPath("Actions.UsedLoadingSlots").OverwriteCollectionInPatch<IList<SlotModel>, IList<Slot>>(patch =>
{
    return patch.Select(slot => new Slot { Id = slot.Id, Name = slot.Name }).ToList();
});
```

## Validation System

The validation system provides fine-grained control over what changes are allowed.

### Validation Function

```csharp
var result = await patchHelper.Patch(original, patch, json, validation =>
{
    // Return true to allow the change, false to reject it
    return ShouldAllowChange(validation);
});
```

### ValidationRequest Properties

The `ValidationRequest` object provides context about the change:

```csharp
public class ValidationRequest
{
    public string Property { get; set; }           // Property name (e.g., "Email")
    public string Path { get; set; }               // Full path (e.g., "User.Contact.Email")
    public object Value { get; set; }              // New value being set
    public ValidationRequestType Type { get; set; } // UpdateProperty, AddItem, RemoveItem
    public object PatchItem { get; set; }          // The patch object
    public object OriginalItem { get; set; }       // The original object
}
```

### Validation Examples

#### Property-Based Validation

```csharp
var result = await patchHelper.Patch(original, patch, json, validation =>
{
    if (validation.Property == "Email")
        return IsValidEmail(validation.Value?.ToString());
    
    if (validation.Property == "Price")
        return (decimal)validation.Value > 0;
    
    return true;
});
```

#### Path-Based Validation

```csharp
var result = await patchHelper.Patch(original, patch, json, validation =>
{
    if (validation.Path.StartsWith("User.Security"))
        return HasSecurityPermission();
    
    if (validation.Path.Contains("Sensitive"))
        return IsAuthorized();
    
    return true;
});
```

#### Operation Type Validation

```csharp
var result = await patchHelper.Patch(original, patch, json, validation =>
{
    switch (validation.Type)
    {
        case ValidationRequestType.AddItem:
            return CanAddItems(validation.Path);
        
        case ValidationRequestType.RemoveItem:
            return CanRemoveItems(validation.Path);
        
        case ValidationRequestType.UpdateProperty:
            return CanUpdateProperty(validation.Path);
        
        default:
            return true;
    }
});
```

#### Default Validation (No Validation)

```csharp
// Pass null to skip validation (allows all changes)
var result = await patchHelper.Patch(original, patch, json, null);
```

## Error Handling

The PatchHelper provides comprehensive error handling with specific exception types.

### Exception Types

#### ArgumentNullException
Thrown when required parameters are null:

```csharp
// These will throw ArgumentNullException:
await patchHelper.Patch(null, patch, json, validation);        // originalDocument is null
await patchHelper.Patch(original, null, json, validation);     // patch is null
await patchHelper.Patch(original, patch, null, validation);    // json is null
```

#### ArgumentOutOfRangeException
Thrown when JSON is empty or whitespace:

```csharp
await patchHelper.Patch(original, patch, "", validation);      // Empty JSON
await patchHelper.Patch(original, patch, "   ", validation);   // Whitespace JSON
```

#### ArgumentException
Thrown when JSON is malformed:

```csharp
var invalidJson = @"{""Name"": ""John"" this is invalid}";
await patchHelper.Patch(original, patch, invalidJson, validation);
```

#### BadRequestException
Thrown for business logic violations:

- Property doesn't exist on target objects
- Validation function returns false
- Invalid GUID format for collection item IDs
- Collection items don't implement IItemId when required

#### SetValueException
Thrown when property assignment fails:

```csharp
// Provides detailed information about the failed property assignment:
// - Property path
// - Value being set and its type
// - Source and target property information
```

### Error Information

Exceptions include detailed context information:

```csharp
try
{
    await patchHelper.Patch(original, patch, json, validation);
}
catch (SetValueException ex)
{
    Console.WriteLine($"Failed to set property: {ex.Message}");
    // Ex.Message includes path, value, and property details
}
catch (BadRequestException ex)
{
    Console.WriteLine($"Business rule violation: {ex.Message}");
}
```

## Best Practices

### 1. Use Type-Safe Validation

```csharp
var result = await patchHelper.Patch(original, patch, json, validation =>
{
    // Use strongly-typed validation
    if (validation.Property == nameof(User.Email))
        return IsValidEmail(validation.Value as string);
    
    return true;
});
```

### 2. Implement Configuration Early

```csharp
// Set up configuration once and reuse
var patchHelper = new PatchHelper(cfg =>
{
    cfg.ForPath("Email").ChangeAction<User, UserRequest>((original, patch) =>
    {
        original.Email = patch.Email?.ToLower();
        original.EmailVerified = false;
        return Task.CompletedTask;
    });
    
    cfg.ForPath("Password").ChangeAction<User, UserRequest>((original, patch) =>
    {
        original.PasswordHash = HashPassword(patch.Password);
        original.PasswordUpdatedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    });
    
    return cfg;
});
```

### 3. Handle Async Operations

The PatchHelper is fully async - use it properly:

```csharp
// Good - properly awaited
var result = await patchHelper.Patch(original, patch, json, validation);

// Bad - blocking async call
var result = patchHelper.Patch(original, patch, json, validation).Result;
```

### 4. Use Meaningful Validation Messages

While not directly supported, you can enhance error handling:

```csharp
var result = await patchHelper.Patch(original, patch, json, validation =>
{
    if (validation.Property == "Age" && (int)validation.Value < 0)
    {
        // Log detailed information before rejecting
        logger.LogWarning($"Invalid age value {validation.Value} for path {validation.Path}");
        return false;
    }
    
    return true;
});
```

### 5. Consider Performance for Large Objects

For large objects with many properties, consider:

```csharp
// Use specific validation rather than checking every property
var result = await patchHelper.Patch(original, patch, json, validation =>
{
    // Only validate properties that need special handling
    var criticalProperties = new[] { "Email", "Password", "SecurityLevel" };
    
    if (criticalProperties.Contains(validation.Property))
        return ValidateCriticalProperty(validation);
    
    // Allow all other properties
    return true;
});
```

## API Reference

### PatchHelper Class

#### Constructors

```csharp
public PatchHelper()
public PatchHelper(PatchConfiguration configuration)
public PatchHelper(Func<PatchConfiguration, PatchConfiguration> func)
```

#### Methods

```csharp
public async Task<T> Patch<T>(
    T originalDocument, 
    object patch, 
    string json, 
    Func<ValidationRequest, bool> validation)
```

### PatchConfiguration Class

#### Methods

```csharp
public PatchConfigurationItem ForPath(string path)
public PatchConfigurationItem ForPath(string start, string end)
public PatchConfigurationCollectionItem ForCollectionPath(string path)
public PatchConfigurationCollectionItem ForCollectionPath(string start, string end)
```

### PatchConfigurationItem Class

#### Methods

```csharp
public void ChangeAction<TOriginal, TPatch>(Func<TOriginal, TPatch, Task> behaviourChange)
```

### PatchConfigurationCollectionItem Class

#### Methods

```csharp
public void ChangeAddItem<TPatchItem, TNewItem>(Func<TPatchItem, TNewItem> addItemFunc)
public void OverwriteCollectionInPatch<TPatchCollection, TOriginalCollection>(
    Func<TPatchCollection, TOriginalCollection> getAsOriginalTypeFunc)
```

### ValidationRequest Class

#### Properties

```csharp
public string Property { get; set; }
public string Path { get; set; }
public object Value { get; set; }
public ValidationRequestType Type { get; set; }
public object PatchItem { get; set; }
public object OriginalItem { get; set; }
```

### ValidationRequestType Enum

```csharp
public enum ValidationRequestType : ushort
{
    UpdateProperty = 0,
    AddItem = 1,
    RemoveItem = 2
}
```

## Examples

### Example 1: Simple User Update

```csharp
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

public class UserRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

// Usage
var user = new User { FirstName = "John", LastName = "Doe", Email = "john@example.com" };
var request = new UserRequest { Email = "john.doe@newdomain.com" };
var json = @"{""Email"": ""john.doe@newdomain.com""}";

var patchHelper = new PatchHelper();
var updatedUser = await patchHelper.Patch(user, request, json, validation => true);

// Result: Only Email is updated, FirstName and LastName remain unchanged
```

### Example 2: Complex Object with Validation

```csharp
var patchHelper = new PatchHelper(cfg =>
{
    cfg.ForPath("Email").ChangeAction<User, UserRequest>((original, patch) =>
    {
        original.Email = patch.Email.ToLower();
        original.EmailUpdatedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    });
    
    return cfg;
});

var result = await patchHelper.Patch(user, request, json, validation =>
{
    if (validation.Property == "Email")
        return IsValidEmail(validation.Value as string);
    
    if (validation.Property == "FirstName" || validation.Property == "LastName")
        return !string.IsNullOrWhiteSpace(validation.Value as string);
    
    return true;
});
```

### Example 3: Collection Management

```csharp
public class Person : IItemId
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public List<PhoneNumber> PhoneNumbers { get; set; } = new();
}

public class PhoneNumber : IItemId
{
    public Guid Id { get; set; }
    public string Number { get; set; }
}

var json = @"{
    ""PhoneNumbers"": [
        {""Id"": ""00000000-0000-0000-0000-000000000000"", ""Number"": ""123-456-7890""},  // New item
        {""Id"": ""existing-guid"", ""Number"": ""updated-number""},                        // Update existing
        {""Id"": ""remove-guid"", ""_remove"": true}                                       // Remove existing
    ]
}";

var result = await patchHelper.Patch(person, request, json, validation => true);
```

### Example 4: Nested Objects

```csharp
public class Employee
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
}

var json = @"{
    ""Address"": {
        ""Street"": ""123 New Street"",
        ""City"": ""Springfield""
    }
}";

var result = await patchHelper.Patch(employee, request, json, validation => true);
// Only Address.Street and Address.City are updated, Address.State remains unchanged
```

### Example 5: Advanced Configuration with Partial Paths

```csharp
var patchHelper = new PatchHelper(cfg =>
{
    // Matches any path ending with "TypeId" that starts with "Items"
    cfg.ForPath("Items", "TypeId").ChangeAction<TransactionItem, TransactionItemRequest>((original, patch) =>
    {
        original.Type = new ItemType { Id = patch.TypeId.Value, Name = "Auto-Generated" };
        return Task.CompletedTask;
    });
    
    // Custom collection item creation
    cfg.ForCollectionPath("", "Items").ChangeAddItem<TransactionItemRequest, TransactionItem>(patch =>
    {
        return patch.TypeId switch
        {
            var id when id == Guid.Parse("group-type-guid") => new GroupTransactionItem(),
            var id when id == Guid.Parse("article-type-guid") => new ArticleTransactionItem(),
            _ => new TransactionItem()
        };
    });
    
    return cfg;
});
```

This comprehensive documentation should provide everything needed to understand and effectively use the PatchHelper in your applications. The helper is designed to handle complex scenarios while maintaining simplicity for basic use cases.