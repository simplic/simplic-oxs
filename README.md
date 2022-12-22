# simplic-oxs


## Using GraphQL in Simplic.Oxs modules

To use GraphQL you need to update your Simplic.Oxs to  **1.0.8922.1222 or higher** and follow these 3 steps.

 Create a class called **Query** inside your Server projects. An example for **shipments** would be as following
 ```cs
public class Query
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
In your Startup.cs inside the **Configure** method use following code snippet `services.UseSimplicGraphQL<Query>();`

 In your Startup.cs inside the **MapHubs** method use following statement `builder.MapGraphQL();`

Now you should be able to start your server and call ***/graphql** in your url.
Example would be `https://localhost:7276/graphql/` 

You can write now queries. 
Example : 

    query{
      shipments{
        createDateTime
      }
    }


