# API Response Examples

## Enhanced Settings with Options

When you call `GET /OrganizationSettings`, the response now includes rich option information with localization keys:

### Enum Setting Response
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
      "displayName": "Simple",
      "displayKey": "settings.user.password.complexity.simple"
    },
    {
      "value": 2,
      "displayName": "Medium",
      "displayKey": "settings.user.password.complexity.medium"
    },
    {
      "value": 3,
      "displayName": "Strong",
      "displayKey": "settings.user.password.complexity.strong"
    },
    {
      "value": 4,
      "displayName": "Enterprise",
      "displayKey": "settings.user.password.complexity.enterprise"
    }
  ]
}
```

### Choice Setting Response
```json
{
  "internalName": "user.notifications.delivery_method",
  "displayName": "Notification Delivery Method",
  "displayKey": "settings.user.notifications.delivery_method",
  "value": "email",
  "defaultValue": "email",
  "valueTypeName": "String",
  "hasOptions": true,
  "options": [
    {
      "value": "email",
      "displayName": "Email",
      "displayKey": "settings.user.notifications.delivery_method.email"
    },
    {
      "value": "sms",
      "displayName": "SMS",
      "displayKey": "settings.user.notifications.delivery_method.sms"
    },
    {
      "value": "push",
      "displayName": "Push Notification",
      "displayKey": "settings.user.notifications.delivery_method.push"
    }
  ]
}
```

### Session Timeout Setting Response
```json
{
  "internalName": "user.session.timeout",
  "displayName": "Session Timeout",
  "displayKey": "settings.user.session.timeout",
  "value": "30m",
  "defaultValue": "30m",
  "valueTypeName": "String",
  "hasOptions": true,
  "options": [
    {
      "value": "15m",
      "displayName": "15 minutes",
      "displayKey": "settings.user.session.timeout.15m"
    },
    {
      "value": "30m",
      "displayName": "30 minutes", 
      "displayKey": "settings.user.session.timeout.30m"
    },
    {
      "value": "1h",
      "displayName": "1 hour",
      "displayKey": "settings.user.session.timeout.1h"
    },
    {
      "value": "8h",
      "displayName": "8 hours",
      "displayKey": "settings.user.session.timeout.8h"
    }
  ]
}
```

## Frontend Usage with Localization

This rich metadata with displayKey enables sophisticated UIs with proper localization:

### Dropdown/Select Components with Default Highlighting
```typescript
// React example with localization and default highlighting
function SettingSelect({ setting, t }: { setting: OrganizationSettingResult, t: TFunction }) {
  if (!setting.hasOptions) {
    return <input type="text" defaultValue={setting.value} />;
  }

  return (
    <select defaultValue={setting.value}>
      {setting.options.map(option => (
        <option 
          key={option.value} 
          value={option.value}
          className={option.value === setting.defaultValue ? 'default-option' : ''}
        >
          {option.displayKey ? t(option.displayKey) : option.displayName}
          {option.value === setting.defaultValue && ' (Default)'}
        </option>
      ))}
    </select>
  );
}
```

### Radio Button Groups with Default Indication
```typescript
function SettingRadioGroup({ setting, t }: { setting: OrganizationSettingResult, t: TFunction }) {
  return (
    <div className="setting-options">
      <h3>{setting.displayKey ? t(setting.displayKey) : setting.displayName}</h3>
      <p className="default-info">
        Default: {setting.options?.find(o => o.value === setting.defaultValue)?.displayName || setting.defaultValue}
      </p>
      {setting.options?.map(option => (
        <label key={option.value} className="radio-option">
          <input 
            type="radio" 
            name={setting.internalName}
            value={option.value}
            defaultChecked={option.value === setting.value}
          />
          <div>
            <strong>
              {option.displayKey ? t(option.displayKey) : option.displayName}
            </strong>
            {option.value === setting.defaultValue && (
              <span className="default-badge">Default</span>
            )}
          </div>
        </label>
      ))}
    </div>
  );
}
```

### Setting Cards with Default Value Display
```typescript
function SettingCard({ setting, t }: { setting: OrganizationSettingResult, t: TFunction }) {
  const currentOption = setting.options?.find(o => o.value === setting.value);
  const defaultOption = setting.options?.find(o => o.value === setting.defaultValue);
  
  return (
    <div className="setting-card">
      <h3>{setting.displayKey ? t(setting.displayKey) : setting.displayName}</h3>
      
      {setting.hasOptions ? (
        <div>
          <div className="current-value">
            <strong>Current:</strong> {currentOption?.displayName || setting.value}
          </div>
          <div className="default-value">
            <strong>Default:</strong> {defaultOption?.displayName || setting.defaultValue}
          </div>
          
          <div className="setting-options">
            {setting.options.map(option => (
              <div 
                key={option.value} 
                className={`option ${option.value === setting.value ? 'selected' : ''} ${option.value === setting.defaultValue ? 'default' : ''}`}
              >
                <h4>{option.displayKey ? t(option.displayKey) : option.displayName}</h4>
              </div>
            ))}
          </div>
        </div>
      ) : (
        <div>
          <div>Current: <input type="text" defaultValue={setting.value} /></div>
          <div>Default: {setting.defaultValue}</div>
        </div>
      )}
    </div>
  );
}
```

### Localization Resource Files
```json
// en.json
{
  "settings.user.password.complexity": "Password Complexity",
  "settings.user.password.complexity.simple": "Simple Security",
  "settings.user.password.complexity.medium": "Balanced Security", 
  "settings.user.password.complexity.strong": "High Security",
  "settings.user.password.complexity.enterprise": "Maximum Security",
  
  "settings.user.notifications.delivery_method": "How to Deliver Notifications",
  "settings.user.notifications.delivery_method.email": "Email Messages",
  "settings.user.notifications.delivery_method.sms": "Text Messages",
  "settings.user.notifications.delivery_method.push": "Mobile Push Alerts"
}
```

```json
// de.json
{
  "settings.user.password.complexity": "Passwort-Komplexität",
  "settings.user.password.complexity.simple": "Einfache Sicherheit",
  "settings.user.password.complexity.medium": "Ausgewogene Sicherheit",
  "settings.user.password.complexity.strong": "Hohe Sicherheit", 
  "settings.user.password.complexity.enterprise": "Maximale Sicherheit",
  
  "settings.user.notifications.delivery_method": "Benachrichtigungsart",
  "settings.user.notifications.delivery_method.email": "E-Mail-Nachrichten",
  "settings.user.notifications.delivery_method.sms": "SMS-Nachrichten",
  "settings.user.notifications.delivery_method.push": "Mobile Push-Benachrichtigungen"
}
```

## Benefits

1. **Cleaner Data Model**: Default value is centrally managed at the setting level, not duplicated in each option
2. **Simpler Logic**: Frontend can compare option values directly with `setting.defaultValue` 
3. **Consistent Pattern**: Follows the same pattern as non-option settings where default is on the setting itself
4. **Better Performance**: Smaller JSON payloads without redundant `isDefault` flags
5. **Less Error-Prone**: No risk of multiple options being marked as default or no default being marked