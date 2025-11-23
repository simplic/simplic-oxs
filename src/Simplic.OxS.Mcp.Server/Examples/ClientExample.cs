using Simplic.OxS.Mcp;
using Simplic.OxS.Mcp.Client;

// Example of how to use the MCP HTTP client
Console.WriteLine("Simplic.OxS MCP Client Example");
Console.WriteLine("================================");

// Configuration
var baseUrl = "https://localhost:5001/mcpsample-api/v1";
var jwtToken = "your-jwt-token-here"; // Replace with actual JWT token

try
{
    using var client = new McpHttpClient(baseUrl, jwtToken);

    // 1. Get server capabilities
    Console.WriteLine("\n1. Getting server capabilities...");
    var capabilities = await client.GetCapabilitiesAsync();
    Console.WriteLine($"Capabilities: {System.Text.Json.JsonSerializer.Serialize(capabilities, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");

    // 2. List available tools
Console.WriteLine("\n2. Listing available tools...");
    var toolList = await client.ListToolsAsync();
    Console.WriteLine($"Found {toolList?.Tools.Count ?? 0} tools:");
 if (toolList?.Tools != null)
    {
    foreach (var tool in toolList.Tools)
        {
       Console.WriteLine($"  - {tool.Name}: {tool.Description}");
        }
 }

    // 3. Call the calculator tool
    Console.WriteLine("\n3. Calling calculator tool (10 + 5)...");
    var calcResult = await client.CallToolAsync("calculate", new Dictionary<string, object>
    {
        ["operation"] = "add",
     ["operand1"] = 10,
        ["operand2"] = 5
    });

    if (calcResult?.Result != null)
    {
    Console.WriteLine($"Calculation successful: {calcResult.Result.IsSuccess}");
     if (calcResult.Result.Content != null)
        {
         foreach (var content in calcResult.Result.Content)
            {
  Console.WriteLine($"Result: {content.Text}");
         }
     }
  }

  // 4. Call the organization info tool
    Console.WriteLine("\n4. Getting organization info...");
  var orgResult = await client.CallToolAsync("get_organization_info", new Dictionary<string, object>
    {
  ["includeDetails"] = true
    });

    if (orgResult?.Result != null)
    {
        Console.WriteLine($"Organization info retrieval: {orgResult.Result.IsSuccess}");
        if (orgResult.Result.Content != null)
        {
  foreach (var content in orgResult.Result.Content)
    {
          Console.WriteLine($"Organization Info: {content.Text}");
     }
        }
 }

    // 5. Check server health
    Console.WriteLine("\n5. Checking server health...");
    var health = await client.GetHealthAsync();
    Console.WriteLine($"Health: {System.Text.Json.JsonSerializer.Serialize(health, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");

}
catch (HttpRequestException ex)
{
    Console.WriteLine($"HTTP Error: {ex.Message}");
    Console.WriteLine("\nMake sure:");
    Console.WriteLine("1. The MCP sample service is running");
    Console.WriteLine("2. The base URL is correct");
    Console.WriteLine("3. You have a valid JWT token");
    Console.WriteLine("4. The JWT token has the required claims (UserId, OrganizationId)");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();