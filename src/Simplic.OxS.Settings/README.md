# Organization Settings Guide

This guide explains how to add new organization-specific settings to your Simplic.OxS microservice.

## Overview

The organization settings system allows you to define configuration values that can be customized per organization. Settings are stored in MongoDB with distributed Redis caching for improved performance.

## Table of Contents

- [Features](#features)
- [Supported Types](#supported-types)
- [Creating Setting Definitions](#creating-setting-definitions)
- [Registering Settings](#registering-settings)
- [Using Settings in Your Service](#using-settings-in-your-service)
- [API Usage](#api-usage)
- [Caching](#caching)
- [Best Practices](#best-practices)
- [Examples](#examples)

## Features

- **Type-safe Settings**: Strongly typed setting definitions with validation
- **MongoDB Storage**: Persistent storage with organization-scoped settings
- **Distributed Caching**: Redis-based caching for improved performance
- **REST API**: Built-in HTTP endpoints for settings management
- **Declarative Registration**: Clean, explicit setting registration in Bootstrap
- **Default Values**: Fallback to defaults when no organization override exists

## Supported Types

The following .NET types are supported for setting values:

- `string`
- `bool`
- `int`, `long`
- `decimal`, `double`, `float`
- `Guid`
- `DateTime`, `DateOnly`, `TimeOnly`
- `Enum` types (with rich display support)
- Nullable versions of all above types

## Enhanced Options Support

The settings system provides rich support for options-based settings with display names and descriptions:

### Enum Settings with Rich Display

Use `EnumOrganizationSettingDefinition<TEnum>` for enum-based settings with enhanced UI support:

```csharp
public enum NotificationPriority
{
    [DisplayName("Low Priority")]
    [Description("Non-urgent notifications, delivered in batches")]
    Low = 1,

    [DisplayName("Normal Priority")]
    [Description("Standard notifications, delivered promptly")]
    Normal = 2,

    [DisplayName("High Priority")]
    [Description("Important notifications, delivered immediately")]
    High = 3,

    [DisplayName("Critical")]
    [Description("Critical alerts, delivered immediately with escalation")]
    Critical = 4
}

public class NotificationPrioritySetting : EnumOrganizationSettingDefinition<NotificationPriority>
{
    public NotificationPrioritySetting() : base(
        internalName: "notifications.priority",
        displayKey: "settings.notifications.priority",
        displayName: "Default Notification Priority",
        defaultValue: NotificationPriority.Normal)
    {
    }
}
```

### Choice Settings with Custom Options

Use `ChoiceOrganizationSettingDefinition` for string-based settings with predefined options:

```csharp
public class ThemeSetting : ChoiceOrganizationSettingDefinition
{
    public ThemeSetting() : base(
        internalName: "ui.theme",
        displayKey: "settings.ui.theme",
        displayName: "User Interface Theme",
        options: new[]
        {
            new SettingOption("light", "Light Theme", "settings.ui.theme.light"),
            new SettingOption("dark", "Dark Theme", "settings.ui.theme.dark"),
            new SettingOption("auto", "Auto", "settings.ui.theme.auto"),
            new SettingOption("high-contrast", "High Contrast", "settings.ui.theme.high_contrast")
        },
        defaultValue: "light")
    {
    }
}
```

### Mixed Settings Example

You can combine different setting types in your service:

```csharp
public class EmailSettings : ChoiceOrganizationSettingDefinition
{
    public EmailSettings() : base(
        internalName: "email.provider",
        displayKey: "settings.email.provider",
        displayName: "Email Provider",
        options: new[]
        {
            new SettingOption("smtp", "SMTP", "settings.email.provider.smtp"),
            new SettingOption("sendgrid", "SendGrid", "settings.email.provider.sendgrid"),
            new SettingOption("ses", "Amazon SES", "settings.email.provider.ses"),
            new SettingOption("disabled", "Disabled", "settings.email.provider.disabled")
        },
        defaultValue: "smtp")
    {
    }
}
```

### Localization Support

The displayKey pattern enables proper internationalization:

```csharp
// The enum will automatically generate displayKeys like:
// settings.notifications.priority.low
// settings.notifications.priority.normal  
// settings.notifications.priority.high
// settings.notifications.priority.critical

public class NotificationPrioritySetting : EnumOrganizationSettingDefinition<NotificationPriority>
{
    public NotificationPrioritySetting() : base(
        internalName: "notifications.priority",
        displayKey: "settings.notifications.priority",
        displayName: "Default Notification Priority",
        defaultValue: NotificationPriority.Normal)
    {
    }
}
```

### Default Value Handling

The default value is managed at the setting level, not on individual options:

```csharp
// For choice settings, explicitly specify which option is the default
public class LogLevelSetting : ChoiceOrganizationSettingDefinition
{
    public LogLevelSetting() : base(
        internalName: "logging.level",
        displayKey: "settings.logging.level",
        displayName: "Log Level",
        options: new[]
        {
            new SettingOption("debug", "Debug", "settings.logging.level.debug"),
            new SettingOption("info", "Information", "settings.logging.level.info"),
            new SettingOption("warn", "Warning", "settings.logging.level.warn"),
            new SettingOption("error", "Error", "settings.logging.level.error")
        },
        defaultValue: "info") // Explicitly set default
    {
    }
}

// For enum settings, the default is set in the base constructor
public class SecurityLevelSetting : EnumOrganizationSettingDefinition<SecurityLevel>
{
    public SecurityLevelSetting() : base(
        internalName: "security.level",
        displayKey: "settings.security.level", 
        displayName: "Security Level",
        defaultValue: SecurityLevel.Medium) // Default handled here
    {
    }
}
```

## Creating Setting Definitions

### Step 1: Create a Setting Definition Class

Create a class that inherits from `OrganizationSettingDefinition<T>`:

```csharp
using Simplic.OxS.Settings.Organization;

public class NotificationEnabledSetting : OrganizationSettingDefinition<bool>
{
    public NotificationEnabledSetting() : base(
        internalName: "notification.enabled",
        displayKey: "settings.notification.enabled",
        displayName: "Enable Notifications",
        defaultValue: true)
    {
    }
}
```

### Step 2: Define Constructor Parameters

- **internalName**: Unique identifier for storage and API access (use dot notation for grouping)
- **displayKey**: Localization key for UI display
- **displayName**: Human-readable name for development/debugging
- **defaultValue**: Value used when no organization override exists

### Step 3: Complex Setting Example

```csharp
public enum NotificationMethod
{
    Email,
    SMS,
    Push
}

public class NotificationMethodSetting : OrganizationSettingDefinition<NotificationMethod>
{
    public NotificationMethodSetting() : base(
        internalName: "notification.method",
        displayKey: "settings.notification.method",
        displayName: "Notification Method",
        defaultValue: NotificationMethod.Email)
    {
    }
}

public class EmailTemplateSetting : OrganizationSettingDefinition<string>
{
    public EmailTemplateSetting() : base(
        internalName: "email.template.welcome",
        displayKey: "settings.email.template.welcome",
        displayName: "Welcome Email Template",
        defaultValue: "default-welcome-template")
    {
    }
}
```

## Registering Settings

### Step 1: Override ConfigureOrganizationSettings in Your Bootstrap

In your service's Bootstrap class, override the `ConfigureOrganizationSettings` method:

```csharp
public class MyServiceBootstrap : Bootstrap
{
    // ... other methods ...

    protected override Action<IOrganizationSettingsBuilder>? ConfigureOrganizationSettings()
    {
        return builder => builder
            .AddSetting<NotificationEnabledSetting>()
            .AddSetting<NotificationMethodSetting>()
            .AddSetting<EmailTemplateSetting>()
            .AddSetting(new CustomSetting("custom.value", "Custom Setting", 42));
    }
}
```

### Registration Methods

- **By Type**: `AddSetting<TDefinition>()` - Creates a new instance
- **By Instance**: `AddSetting(definition)` - Uses provided instance

## Using Settings in Your Service

### Method 1: Typed Access (Recommended)

```csharp
public class NotificationService
{
    private readonly IOrganizationSettingsProvider settingsProvider;

    public NotificationService(IOrganizationSettingsProvider settingsProvider)
    {
        this.settingsProvider = settingsProvider;
    }

    public async Task<bool> ShouldSendNotificationAsync()
    {
        var setting = await settingsProvider.GetAsync<NotificationEnabledSetting, bool>();
        return setting.Value ?? setting.DefaultValue ?? false;
    }

    public async Task<NotificationMethod> GetNotificationMethodAsync()
    {
        var setting = await settingsProvider.GetAsync<NotificationMethodSetting, NotificationMethod>();
        return setting.Value ?? setting.DefaultValue ?? NotificationMethod.Email;
    }
}
```

### Method 2: String-based Access

```csharp
public async Task<bool> IsFeatureEnabledAsync(string featureName)
{
    var setting = await settingsProvider.GetAsync($"feature.{featureName}.enabled");
    return (bool?)setting.Value ?? false;
}
```

### Method 3: Get All Settings

```csharp
public async Task<Dictionary<string, object?>> GetAllSettingsAsync()
{
    var settings = await settingsProvider.GetAllAsync();
    return settings.ToDictionary(s => s.InternalName, s => s.Value);
}
```

## API Usage

The settings are automatically exposed via REST API when you include the organization settings in your service.

### Get All Settings
```http
GET /OrganizationSettings
Authorization: Bearer {jwt-token}
```

### Get Specific Setting
```http
GET /OrganizationSettings/{internalName}
Authorization: Bearer {jwt-token}
```

### Update Setting
```http
PUT /OrganizationSettings/{internalName}
Authorization: Bearer {jwt-token}
Content-Type: application/json

{
  "value": true
}
```

## Caching

The organization settings system includes built-in distributed caching for optimal performance:

### Cache Strategy

- **Individual Settings**: Cached for 15 minutes per organization
- **All Settings**: Cached for 5 minutes per organization  
- **Cache Keys**: Scoped by organization ID to prevent cross-organization data leaks
- **Cache Invalidation**: Automatic invalidation on setting updates

### Cache Behavior

1. **Read Operations**:
   - Check Redis cache first
   - Fall back to MongoDB on cache miss
   - Store result in cache for future requests

2. **Write Operations**:
   - Update MongoDB
   - Invalidate both individual and bulk caches
   - Next read will refresh cache from database

### Cache Keys Format

Cache keys are scoped by service name and organization ID to prevent conflicts in shared Redis instances:

- Individual setting: `org_setting:{serviceName}:{organizationId}:{internalName}`
- All settings: `org_settings_all:{serviceName}:{organizationId}`

**Example cache keys:**
- `org_setting:UserManagement:12345678-1234-1234-1234-123456789012:notification.enabled`
- `org_settings_all:PaymentService:12345678-1234-1234-1234-123456789012`

This scoping ensures that:
- Different microservices can share the same Redis instance without conflicts
- Organizations are isolated from each other
- Service-specific settings don't interfere with each other

### Performance Benefits

- **Reduced Database Load**: Frequently accessed settings served from cache
- **Improved Response Times**: Redis reads are significantly faster than MongoDB
- **Scalability**: Cache reduces database pressure during high traffic

### Failure Resilience

- Cache failures are logged but don't break functionality
- Automatic fallback to database when cache is unavailable
- Settings system remains operational even if Redis is down

## Best Practices

### 1. Naming Conventions
- Use dot notation for hierarchical grouping: `email.smtp.host`
- Use lowercase with dots: `notification.enabled` not `NotificationEnabled`
- Group related settings: `payment.gateway.url`, `payment.gateway.timeout`

### 2. Default Values
- Always provide sensible defaults
- Defaults should represent the "safe" or "most common" configuration
- Consider backwards compatibility when changing defaults

### 3. Internal Names
- Keep them stable - changing internal names breaks existing configurations
- Make them descriptive but concise
- Avoid special characters except dots and hyphens

### 4. Display Information
- Use proper localization keys for `displayKey`
- Make `displayName` user-friendly for administrative interfaces
- Consider grouping in UI by using consistent prefixes

### 5. Type Safety
- Prefer enums over strings for predefined values
- Use appropriate numeric types (don't use `decimal` for counts)
- Consider nullable types if the setting can be "unset"

### 6. Caching Considerations
- Settings are cached automatically - no manual cache management needed
- Cache invalidation is handled on updates
- Consider cache duration when planning setting update frequency
- Monitor Redis health for optimal performance

## Examples

### Complete Feature Toggle Example

```csharp
// 1. Define the setting
public class AdvancedReportingSetting : OrganizationSettingDefinition<bool>
{
    public AdvancedReportingSetting() : base(
        internalName: "features.advanced.reporting",
        displayKey: "settings.features.advanced.reporting",
        displayName: "Enable Advanced Reporting",
        defaultValue: false)
    {
    }
}

// 2. Register in Bootstrap
protected override Action<IOrganizationSettingsBuilder>? ConfigureOrganizationSettings()
{
    return builder => builder
        .AddSetting<AdvancedReportingSetting>();
}

// 3. Use in service
public class ReportingService
{
    private readonly IOrganizationSettingsProvider settingsProvider;

    public ReportingService(IOrganizationSettingsProvider settingsProvider)
    {
        this.settingsProvider = settingsProvider;
    }

    public async Task<bool> CanGenerateAdvancedReportAsync()
    {
        var setting = await settingsProvider.GetAsync<AdvancedReportingSetting, bool>();
        return setting.Value ?? setting.DefaultValue ?? false;
    }
}
```

### Configuration Settings Example

```csharp
// Connection timeout setting
public class DatabaseTimeoutSetting : OrganizationSettingDefinition<int>
{
    public DatabaseTimeoutSetting() : base(
        internalName: "database.connection.timeout",
        displayKey: "settings.database.connection.timeout",
        displayName: "Database Connection Timeout (seconds)",
        defaultValue: 30)
    {
    }
}

// API endpoint setting
public class ExternalApiEndpointSetting : OrganizationSettingDefinition<string>
{
    public ExternalApiEndpointSetting() : base(
        internalName: "external.api.endpoint",
        displayKey: "settings.external.api.endpoint",
        displayName: "External API Endpoint",
        defaultValue: "https://api.example.com/v1")
    {
    }
}
```

## Error Handling

The system throws specific exceptions for common scenarios:

- `SettingNotFoundException`: Setting not registered
- `SettingTypeMismatchException`: Wrong value type provided
- `SettingValueNullException`: Null value for non-nullable setting
- `PersistenceUnavailableException`: Database connectivity issues

Always handle these appropriately in your services:

```csharp
try 
{
    var setting = await settingsProvider.GetAsync<MySetting, string>();
    return setting.Value ?? setting.DefaultValue;
}
catch (SettingNotFoundException)
{
    // Log warning and return fallback value
    logger.LogWarning("Setting not found, using fallback value");
    return "fallback-value";
}
```

## Testing

For unit testing, you can mock `IOrganizationSettingsProvider`:

```csharp
var mockProvider = new Mock<IOrganizationSettingsProvider>();
mockProvider
    .Setup(p => p.GetAsync<NotificationEnabledSetting, bool>())
    .ReturnsAsync(new OrganizationSettingResult<bool>(
        "notification.enabled", 
        "Enable Notifications", 
        "settings.notification.enabled",
        true, // current value
        false // default value
    ));
```

## Migration Considerations

When updating existing settings:

1. **Never change internal names** - this breaks existing configurations
2. **Default value changes** affect organizations that haven't overridden the setting
3. **Type changes** require data migration and are generally not recommended
4. **Removing settings** should be done gradually with deprecation warnings

---

For more examples, see the `Simplic.OxS.SettingsSample` project in the solution.