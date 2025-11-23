using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Simplic.OxS.Mcp.Client;

/// <summary>
/// Simple HTTP client for connecting to Simplic.OxS MCP servers
/// </summary>
public class McpHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the McpHttpClient class
    /// </summary>
    /// <param name="baseUrl">Base URL of the MCP server (e.g., "https://localhost:5001/mcpsample-api/v1")</param>
    /// <param name="jwtToken">JWT token for authentication</param>
    public McpHttpClient(string baseUrl, string jwtToken)
    {
   _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
   _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Gets server capabilities
    /// </summary>
public async Task<object?> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/mcp/capabilities", cancellationToken);
      response.EnsureSuccessStatusCode();
    
 var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<object>(content);
 }

    /// <summary>
    /// Lists available tools
    /// </summary>
    public async Task<McpToolListResponse?> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"{_baseUrl}/mcp/tools/list", 
new StringContent("{}", Encoding.UTF8, "application/json"), cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<McpToolListResponse>(content, new JsonSerializerOptions
        {
          PropertyNameCaseInsensitive = true
        });
  }

    /// <summary>
    /// Calls an MCP tool
    /// </summary>
    /// <param name="toolName">Name of the tool to call</param>
    /// <param name="arguments">Tool arguments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<McpToolResponse?> CallToolAsync(
      string toolName, 
        Dictionary<string, object>? arguments = null,
        CancellationToken cancellationToken = default)
    {
  var request = new McpToolRequest
        {
      Method = "tools/call",
    Params = new McpToolRequestParams
     {
          Name = toolName,
          Arguments = arguments
}
        };

    var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
   {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var response = await _httpClient.PostAsync($"{_baseUrl}/mcp/tools/call",
   new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken);
        
  var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
      throw new HttpRequestException($"MCP tool call failed: {response.StatusCode} - {content}");
        }

        return JsonSerializer.Deserialize<McpToolResponse>(content, new JsonSerializerOptions
{
     PropertyNameCaseInsensitive = true
        });
}

    /// <summary>
    /// Gets server health status
    /// </summary>
    public async Task<object?> GetHealthAsync(CancellationToken cancellationToken = default)
    {
 var response = await _httpClient.GetAsync($"{_baseUrl}/mcp/health", cancellationToken);
        response.EnsureSuccessStatusCode();
        
var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<object>(content);
    }

    /// <summary>
    /// Disposes the HTTP client
    /// </summary>
    public void Dispose()
    {
   _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Response model for tool list
/// </summary>
public class McpToolListResponse
{
    public List<McpTool> Tools { get; set; } = new();
}