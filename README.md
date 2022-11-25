# simplic-oxs


## Using GraphQL in Simplic.Oxs modules

To use GraphQL you need to update your Simplic.Oxs to  &&VersionNumber&& and follow these 3 steps.

 1. In your Startup.cs inside the **Configure** method use following code snippet `services.UseSimplicGraphQL()`

 2. In your Startup.cs inside the **MapHubs** method use following statement `builder.MapGraphQL`
 3. The last step you need to create a class that implements the IQueryBase interface. An example would be: 
```cs
public class Query : IQueryBase
	{
		[HotChocolate.AspNetCore.Authorization.AuthorizeAttribute]
		[UseProjection]
		[UseFiltering]
		public async Task<IExecutable<Shipment>> GetShipments([Service] IShipmentRepository shipmentRepository)
		{
			return await shipmentRepository.GetCollection();
		}
	}
```
