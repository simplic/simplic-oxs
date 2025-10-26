# API Response Examples

## Enhanced Settings with Options

When you call `GET /OrganizationSettings`, the response includes rich option information following the established naming patterns:

### Grouped Settings Response
```json
[
  {
    "internalName": "shipment-number-unique",
    "displayName": "Shipment number should be unique",
    "displayKey": "logistics.settings.shipmentNumberUnique.displayKey",
    "value": true,
    "defaultValue": false,
    "valueTypeName": "Boolean",
    "options": null,
    "hasOptions": false,
    "groupKey": "shipment",
    "groupDisplayKey": "logistics.settings.groups.shipment.displayKey",
    "groupDisplayName": "Shipment Settings"
  }
]
```

### String Setting Response
```json
{
  "internalName": "shipment-sequence-number",
  "displayName": "Shipment sequence number",
  "displayKey": "logistics.settings.shipmentSequenceNumber.displayKey",
  "value": "sequence_number_shipment",
  "defaultValue": "sequence_number_shipment",
  "valueTypeName": "String",
  "options": null,
  "hasOptions": false
}
```

### Enum Setting Response with Custom Attributes
```json
{
  "internalName": "shipment-sequence-number-date",
  "displayName": "Shipment Sequence Number Date",
  "displayKey": "logistics.settings.shipmentSequenceNumberDate.displayKey",
  "value": 2,
  "defaultValue": 0,
  "valueTypeName": "ShipmentSequenceNumberDates",
  "hasOptions": true,
  "options": [
    {
      "value": 0,
      "displayName": "Loading Date",
      "displayKey": "logistics.settings.shipmentSequenceNumberDate.loadingDate.displayKey"
    },
    {
      "value": 1,
      "displayName": "Delivery Date",
      "displayKey": "logistics.settings.shipmentSequenceNumberDate.deliveryDate.displayKey"
    },
    {
      "value": 2,
      "displayName": "Order Date",
      "displayKey": "logistics.settings.shipmentSequenceNumberDate.orderDate.displayKey"
    }
  ]
}
```

### Choice Setting Response
```json
{
  "internalName": "preferred-carrier",
  "displayName": "Preferred shipping carrier",
  "displayKey": "logistics.settings.preferredCarrier.displayKey",
  "value": "dhl",
  "defaultValue": "dhl",
  "valueTypeName": "String",
  "hasOptions": true,
  "options": [
    {
      "value": "dhl",
      "displayName": "DHL Express",
      "displayKey": "logistics.settings.preferredCarrier.dhl.displayKey"
    },
    {
      "value": "fedex",
      "displayName": "FedEx",
      "displayKey": "logistics.settings.preferredCarrier.fedex.displayKey"
    },
    {
      "value": "ups",
      "displayName": "UPS",
      "displayKey": "logistics.settings.preferredCarrier.ups.displayKey"
    },
    {
      "value": "custom",
      "displayName": "Custom Carrier",
      "displayKey": "logistics.settings.preferredCarrier.custom.displayKey"
    }
  ]
}
```

### Numeric Setting Response
```json
{
  "internalName": "max-package-weight-kg",
  "displayName": "Maximum package weight (kg)",
  "displayKey": "logistics.settings.maxPackageWeight.displayKey",
  "value": 30.0,
  "defaultValue": 30.0,
  "valueTypeName": "Decimal",
  "options": null,
  "hasOptions": false
}
```

### Complex Enum with Custom Display Keys
```json
{
  "internalName": "cost-calculation-method",
  "displayName": "Cost calculation method",
  "displayKey": "logistics.settings.costCalculationMethod.displayKey",
  "value": 0,
  "defaultValue": 0,
  "valueTypeName": "CostCalculationMethod",
  "hasOptions": true,
  "options": [
    {
      "value": 0,
      "displayName": "Weight Based",
      "displayKey": "logistics.settings.costCalculation.weightBased.displayKey"
    },
    {
      "value": 1,
      "displayName": "Volume Based",
      "displayKey": "logistics.settings.costCalculation.volumeBased.displayKey"
    },
    {
      "value": 2,
      "displayName": "Distance Based",
      "displayKey": "logistics.settings.costCalculation.distanceBased.displayKey"
    },
    {
      "value": 3,
      "displayName": "Hybrid Calculation",
      "displayKey": "logistics.settings.costCalculation.hybrid.displayKey"
    }
  ]
}
```

## Frontend Usage with Organized Localization

The hierarchical display keys enable better organization of localization resources:

### Hierarchical Localization Resource Files
```json
// en.json - Organized by feature/domain
{
  "logistics": {
    "settings": {
      "shipmentNumberUnique": {
        "displayKey": "Shipment Number Uniqueness"
      },
      "shipmentSequenceNumber": {
        "displayKey": "Sequence Number Pattern"
      },
      "shipmentSequenceNumberDate": {
        "displayKey": "Date for Sequence Numbering",
        "loadingDate": {
          "displayKey": "Use Loading Date"
        },
        "deliveryDate": {
          "displayKey": "Use Delivery Date"
        },
        "orderDate": {
          "displayKey": "Use Order Date"
        }
      },
      "preferredCarrier": {
        "displayKey": "Default Shipping Carrier",
        "dhl": {
          "displayKey": "DHL Express Worldwide"
        },
        "fedex": {
          "displayKey": "Federal Express"
        },
        "ups": {
          "displayKey": "United Parcel Service"
        },
        "custom": {
          "displayKey": "Custom/Other Carrier"
        }
      },
      "maxPackageWeight": {
        "displayKey": "Weight Limit per Package"
      },
      "costCalculation": {
        "weightBased": {
          "displayKey": "Calculate by Weight"
        },
        "volumeBased": {
          "displayKey": "Calculate by Volume"
        },
        "distanceBased": {
          "displayKey": "Calculate by Distance"
        },
        "hybrid": {
          "displayKey": "Hybrid Calculation Method"
        }
      }
    }
  }
}
```

### React Component with Hierarchical Keys
```typescript
function SettingSelect({ setting, t }: { setting: OrganizationSettingResult, t: TFunction }) {
  if (!setting.hasOptions) {
    return (
      <div className="setting-container">
        <label>{t(setting.displayKey)}</label>
        <input type="text" defaultValue={setting.value} />
      </div>
    );
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
            {t(option.displayKey)}
            {option.value === setting.defaultValue && ' (Default)'}
          </option>
        ))}
      </select>
    </div>
  );
}
```

### Bootstrap Configuration Example
```csharp
protected override Action<IOrganizationSettingsBuilder>? ConfigureOrganizationSettings()
{
    return builder => builder
        .AddSetting<ShipmentNumberUniqueSetting>()
        .AddSetting<ShipmentSequenceNumberSetting>()
        .AddSetting<ShipmentSequenceNumberDateSetting>()
        .AddSetting<PreferredCarrierSetting>()
        .AddSetting<MaxPackageWeightSetting>()
        .AddSetting<CostCalculationMethodSetting>();
}
```

## Naming Conventions

The examples follow these established patterns:

### Internal Names
- **Format**: `kebab-case` with descriptive names
- **Examples**: `shipment-number-unique`, `max-package-weight-kg`, `cost-calculation-method`

### Display Keys  
- **Format**: Hierarchical dot notation following domain structure
- **Pattern**: `{domain}.settings.{feature}.{specifics}.displayKey`
- **Examples**: 
  - `logistics.settings.shipmentNumberUnique.displayKey`
  - `logistics.settings.costCalculation.weightBased.displayKey`

### Display Names
- **Format**: Human-readable, descriptive names
- **Examples**: "Shipment number should be unique", "Weight Based", "Maximum package weight (kg)"

## Benefits of This Structure

1. **Consistent Naming**: All settings follow the same kebab-case pattern for internal names
2. **Hierarchical Organization**: Display keys are organized by domain and feature
3. **Localization Ready**: Structure supports easy translation and regional customization
4. **API Friendly**: RESTful endpoint paths align with internal naming conventions
5. **Developer Friendly**: Clear, descriptive names make code self-documenting
6. **Scalable**: Structure supports growth as more settings are added