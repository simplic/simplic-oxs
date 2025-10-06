# API Response Examples

## Enhanced Settings with Options

When you call `GET /OrganizationSettings`, the response now includes rich option information with custom localization keys and display names:

### Enum Setting Response with Custom Attributes
```json
{
  "internalName": "user.password.complexity",
  "displayName": "Password Complexity Requirements",
  "displayKey": "settings.user.password.complexity",
  "value": 2,
  "defaultValue": 2,
  "valueTypeName": "PasswordComplexity",
  "hasOptions": true,
  "options": [
    {
      "value": 1,
      "displayName": "Basic Security Level",
      "displayKey": "security.password.basic"
    },
    {
      "value": 2,
      "displayName": "Standard Security Level",
      "displayKey": "security.password.standard"
    },
    {
      "value": 3,
      "displayName": "Enhanced Security Level", 
      "displayKey": "security.password.enhanced"
    },
    {
      "value": 4,
      "displayName": "Maximum Security Level",
      "displayKey": "security.password.maximum"
    }
  ]
}
```

### Mixed Attribute Usage Example
```json
{
  "internalName": "notifications.priority",
  "displayName": "Default Notification Priority",
  "displayKey": "settings.notifications.priority",
  "value": 2,
  "defaultValue": 2,
  "valueTypeName": "NotificationPriority",
  "hasOptions": true,
  "options": [
    {
      "value": 1,
      "displayName": "Low Priority Notifications",
      "displayKey": "notifications.priority.low"
    },
    {
      "value": 2,
      "displayName": "Normal Priority",
      "displayKey": "settings.notifications.priority.normal"
    },
    {
      "value": 3,
      "displayName": "High Priority Notifications",
      "displayKey": "notifications.priority.urgent"
    },
    {
      "value": 4,
      "displayName": "Critical",
      "displayKey": "settings.notifications.priority.critical"
    }
  ]
}
```

### Theme Preference with All Attribute Combinations
```json
{
  "internalName": "ui.theme.preference",
  "displayName": "Theme Preference",
  "displayKey": "settings.ui.theme.preference",
  "value": 1,
  "defaultValue": 1,
  "valueTypeName": "ThemePreference",
  "hasOptions": true,
  "options": [
    {
      "value": 1,
      "displayName": "Light Theme",
      "displayKey": "ui.theme.light"
    },
    {
      "value": 2,
      "displayName": "Dark Theme",
      "displayKey": "ui.theme.dark"
    },
    {
      "value": 3,
      "displayName": "Automatic Theme Selection",
      "displayKey": "settings.ui.theme.preference.auto"
    },
    {
      "value": 4,
      "displayName": "High Contrast",
      "displayKey": "ui.theme.contrast"
    },
    {
      "value": 5,
      "displayName": "Custom",
      "displayKey": "settings.ui.theme.preference.custom"
    }
  ]
}
```

## Frontend Usage with Organized Localization

The custom attributes enable better organization of localization resources and cleaner display names:

### Hierarchical Localization Resource Files
```json
// en.json - Organized by feature/domain
{
  // Security-related translations (custom keys)
  "security": {
    "levels": {
      "basic": "Basic Protection Level",
      "standard": "Standard Protection Level",
      "enhanced": "Enhanced Protection Level", 
      "maximum": "Maximum Protection Level"
    },
    "password": {
      "basic": "Simple Requirements",
      "standard": "Standard Requirements", 
      "enhanced": "Strong Requirements",
      "maximum": "Enterprise Requirements"
    }
  },
  
  // UI-related translations (custom keys)
  "ui": {
    "theme": {
      "light": "Bright Interface",
      "dark": "Dark Interface",
      "contrast": "High Contrast Interface"
    }
  },
  
  // Notification-specific translations (custom keys)
  "notifications": {
    "priority": {
      "low": "Background Notifications",
      "urgent": "Important Notifications"
    }
  },
  
  // Standard settings translations (auto-generated keys)
  "settings": {
    "ui": {
      "theme": {
        "preference": {
          "auto": "System Default Theme",
          "custom": "User Customized Theme"
        }
      }
    },
    "notifications": {
      "priority": {
        "normal": "Regular Notifications",
        "critical": "Emergency Alerts"
      }
    }
  }
}
```

### React Component with Attribute-Driven Display
```typescript
function SettingSelect({ setting, t }: { setting: OrganizationSettingResult, t: TFunction }) {
  if (!setting.hasOptions) {
    return <input type="text" defaultValue={setting.value} />;
  }

  return (
    <div className="setting-container">
      <label>{t(setting.displayKey)}</label>
      <select defaultValue={setting.value}>
        {setting.options.map(option => (
          <option 
            key={option.value} 
            value={option.value}
            className={option.value === setting.defaultValue ? 'default-option' : ''}
          >
            {/* Use the localized version if available, fallback to display name */}
            {t(option.displayKey, { defaultValue: option.displayName })}
            {option.value === setting.defaultValue && ' (Default)'}
          </option>
        ))}
      </select>
    </div>
  );
}
```

## Display Name Priority Demonstration

The following shows how different attribute combinations affect the final display name:

```csharp
public enum ExampleEnum
{
    [SettingDisplayName("Custom Display Name")]
    [Description("Description Text")]
    Option1 = 1,  // Result: "Custom Display Name"

    [Description("Only Description")]  
    Option2 = 2,  // Result: "Only Description"

    Some_Enum_Value = 3  // Result: "Some Enum Value" (formatted)
}
```

**Resulting JSON:**
```json
{
  "options": [
    {
      "value": 1,
      "displayName": "Custom Display Name"  // From SettingDisplayName
    },
    {
      "value": 2, 
      "displayName": "Only Description"     // From Description
    },
    {
      "value": 3,
      "displayName": "Some Enum Value"      // Formatted enum name
    }
  ]
}
```

## Benefits of Dedicated Attributes

1. **Clear Intent**: `SettingDisplayName` vs `SettingDisplayKey` make purposes explicit
2. **Better Fallbacks**: Multiple options for display names with clear priority
3. **Organized Keys**: Custom keys enable better localization file organization
4. **Flexibility**: Use attributes only where needed, rely on auto-generation elsewhere
5. **IntelliSense Support**: IDE can provide better code completion and validation
6. **Maintainability**: Easy to see what's custom vs auto-generated at a glance