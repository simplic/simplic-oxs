using System.ComponentModel;

namespace Simplic.OxS.Mcp.Server.Examples;

/// <summary>
/// Example MCP tool for getting organization information
/// </summary>
public class OrganizationInfoTool : McpToolBase
{
    /// <summary>
/// Gets organization information
 /// </summary>
    /// <param name="includeDetails">Whether to include detailed information</param>
    /// <param name="context">Request context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Organization information</returns>
    [McpTool("get_organization_info", "Retrieves information about the current organization")]
    public async Task<McpToolResult> GetOrganizationInfo(
        [Description("Whether to include detailed information about the organization")]
        bool includeDetails = false,
 IRequestContext context = null!,
        CancellationToken cancellationToken = default)
    {
        try
   {
        if (context.OrganizationId == null)
   {
         return McpToolResult.Failed("No organization context available");
  }

            var basicInfo = new
            {
       OrganizationId = context.OrganizationId.Value,
          UserId = context.UserId ?? Guid.Empty,
    CorrelationId = context.CorrelationId ?? Guid.Empty,
        Timestamp = DateTime.UtcNow
        };

          if (!includeDetails)
       {
      return McpToolResult.Success($"Organization ID: {basicInfo.OrganizationId}");
            }

      var detailedInfo = new
        {
            basicInfo.OrganizationId,
                basicInfo.UserId,
        basicInfo.CorrelationId,
basicInfo.Timestamp,
    Details = new
    {
Description = "Current organization context for MCP requests",
 ActiveSessions = 1,
      LastActivity = DateTime.UtcNow
        }
       };

  var content = System.Text.Json.JsonSerializer.Serialize(detailedInfo, new System.Text.Json.JsonSerializerOptions
          {
    WriteIndented = true
            });

            return McpToolResult.Success(content);
        }
        catch (Exception ex)
    {
            return McpToolResult.Failed($"Error retrieving organization info: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<McpToolResult> ExecuteAsync(
        IDictionary<string, object>? arguments,
 IRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var includeDetails = false;

        if (arguments?.ContainsKey("includeDetails") == true)
        {
 if (arguments["includeDetails"] is bool details)
    includeDetails = details;
  else if (arguments["includeDetails"] is string detailsStr)
          bool.TryParse(detailsStr, out includeDetails);
        }

 return await GetOrganizationInfo(includeDetails, context, cancellationToken);
    }
}