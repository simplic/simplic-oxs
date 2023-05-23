# simplic-oxs


## Using GraphQL in Simplic.OxS modules

To use GraphQL you need to update your Simplic.OxS.Server to  **1.0.8922.1222 or higher** and follow these 3 steps.

 Create a class called **Query** inside your Server projects. An example for **workshops** would be as following
 ```cs
public class Query
	{
		[HotChocolate.AspNetCore.Authorization.AuthorizeAttribute]
		[UseProjection]
		[UseFiltering]
		public async Task<IExecutable<Workshop>> GetWorkshops([Service] IWorkshopRepository workshopRepository)
		{
			return await workshopRepository.GetCollection();
		}
	}
```
In your Startup.cs inside the **Configure** method use following code snippet `services.UseGraphQL<Query>();`

 In your Startup.cs inside the **MapHubs** method use following statement `builder.MapGraphQL();`

Now you should be able to start your server and call ***/graphql** in your url.
Example would be `https://localhost:7276/graphql/` 

You can write now queries. 
Example : 

    query {
	  workshops {
		id
		createUserId
		kilometre
		plannedDate
		operatingHours
		vehicle {
		  id
		  matchCode
		}
		items{
		  workToDo
		  workDone
		  timeInMinutes
		  articles{
			id
		    name
			number
			quantity
		  }
		  tags{
			internalName
		  }
		}
	  }
	}

## Using GraphQL in Simplic.OxS modules with paging
To use paging in GraphQL you need to update your Simplic.OxS.Data to  **1.0.5423.417 or higher**, Simplic.OxS.Data.MongoDb to **1.0.4223.417 or higher** and Simplic.OxS.Server to **1.2.123.417 or higher**.

Update the method in the class **Query**, in which you want to use paging.
An example for using paging **workshops** would be as following
 ```cs
public class Query
	{
		[HotChocolate.AspNetCore.Authorization.AuthorizeAttribute]
		[UsePaging]
		[UseProjection]
		[UseFiltering]
		public async Task<IExecutable<Workshop>> GetWorkshops([Service] IWorkshopRepository workshopRepository)
		{
			return await workshopRepository.GetCollection();
		}
	}
```

Also you can use **UseOffsetPaging** instead of **UsePaging**.

It is important to keep the order.<br>**Paging -> Projection -> Filtering**

Now you can write a query with paging.

An example for cursor-based-paging:

	query GetWorkshops($firstVar: Int!, $lastVar: Int!, $statusVar: String!) {
  	  workshops(
    	first: $firstVar
    	last: $lastVar
    	where: { status: { number: { eq: $statusVar } } }
  	  ) {
    	nodes {
      	  id
      	  createUserId
      	  kilometre
      	  plannedDate
      	  operatingHours
      	  vehicle {
        	id
        	matchCode
      	  }
          items {
        	workToDo
        	workDone
        	timeInMinutes
        	articles {
          	  id
          	  name
          	  number
          	  quantity
        	}
        	tags {
          	  internalName
       		}
      	  }
      	  status {
            name
      	  }
        }
    	pageInfo {
      	  hasNextPage
      	  hasPreviousPage
		  startCursor
      	  endCursor
    	}
  	  }
	}

Also you can use UseOffsetPaging instead of UsePaging.

An example for offset paging:

	query GetWorkshops($skipVar: Int!, $takeVar: Int!, $statusVar: String!) {
  	  workshops(
    	skip: $skipVar
    	take: $takeVar
    	where: { status: { number: { eq: $statusVar } } }
  	  ) {
    	items {
      	  id
      	  createUserId
      	  kilometre
      	  plannedDate
      	  operatingHours
      	  vehicle {
        	id
        	matchCode
      	  }
          items {
        	workToDo
        	workDone
        	timeInMinutes
        	articles {
          	  id
          	  name
          	  number
          	  quantity
        	}
        	tags {
          	  internalName
       		}
      	  }
      	  status {
            name
      	  }
        }
    	pageInfo {
      	  hasNextPage
      	  hasPreviousPage
    	}
  	  }
	}


With a ? after a property you can define this property to nullable.
Banana Cake pop display a error but its still work.
It is important to use this in querys when something can be null.
