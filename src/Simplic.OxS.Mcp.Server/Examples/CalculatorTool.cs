using System.ComponentModel;
using System.Text.Json;

namespace Simplic.OxS.Mcp.Server.Examples;

/// <summary>
/// Example MCP tool for performing calculations
/// </summary>
public class CalculatorTool : McpToolBase
{
    /// <summary>
    /// Performs a mathematical calculation
  /// </summary>
  /// <param name="operation">The operation to perform (add, subtract, multiply, divide)</param>
    /// <param name="operand1">First operand</param>
    /// <param name="operand2">Second operand</param>
    /// <param name="context">Request context</param>
    /// <param name="cancellationToken">Cancellation token</param>
/// <returns>Calculation result</returns>
    [McpTool("calculate", "Performs basic mathematical calculations")]
    public async Task<McpToolResult> Calculate(
        [Description("The mathematical operation to perform")]
string operation,
        [Description("The first number for the calculation")]
        double operand1,
   [Description("The second number for the calculation")]
        double operand2,
      IRequestContext context = null!,
    CancellationToken cancellationToken = default)
 {
    try
{
  double result = operation.ToLowerInvariant() switch
            {
     "add" or "+" => operand1 + operand2,
  "subtract" or "-" => operand1 - operand2,
       "multiply" or "*" => operand1 * operand2,
        "divide" or "/" => operand2 != 0 ? operand1 / operand2 : throw new DivideByZeroException("Cannot divide by zero"),
       _ => throw new ArgumentException($"Unknown operation: {operation}")
     };

            var resultData = new
   {
     Operation = operation,
   Operand1 = operand1,
            Operand2 = operand2,
      Result = result,
 CalculatedBy = $"User {context.UserId} in Organization {context.OrganizationId}",
     Timestamp = DateTime.UtcNow
            };

 var json = JsonSerializer.Serialize(resultData, new JsonSerializerOptions { WriteIndented = true });
         return McpToolResult.Success(json);
        }
   catch (Exception ex)
        {
 return McpToolResult.Failed($"Calculation error: {ex.Message}");
 }
  }

    /// <inheritdoc />
    public override async Task<McpToolResult> ExecuteAsync(
        IDictionary<string, object>? arguments,
        IRequestContext context,
        CancellationToken cancellationToken = default)
    {
 if (arguments == null)
         return McpToolResult.Failed("Arguments are required for calculation");

 if (!arguments.TryGetValue("operation", out var operationObj) || operationObj is not string operation)
     return McpToolResult.Failed("Operation parameter is required and must be a string");

     if (!arguments.TryGetValue("operand1", out var operand1Obj))
       return McpToolResult.Failed("Operand1 parameter is required");

        if (!arguments.TryGetValue("operand2", out var operand2Obj))
         return McpToolResult.Failed("Operand2 parameter is required");

        // Convert operands to double
        if (!TryConvertToDouble(operand1Obj, out var operand1))
return McpToolResult.Failed("Operand1 must be a valid number");

    if (!TryConvertToDouble(operand2Obj, out var operand2))
       return McpToolResult.Failed("Operand2 must be a valid number");

return await Calculate(operation, operand1, operand2, context, cancellationToken);
    }

    /// <summary>
    /// Attempts to convert an object to double
  /// </summary>
private static bool TryConvertToDouble(object? value, out double result)
    {
  result = 0;

     return value switch
 {
    double d => (result = d) == d,
           float f => (result = f) == f,
   int i => (result = i) == i,
     long l => (result = l) == l,
    string s => double.TryParse(s, out result),
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.TryGetDouble(out result),
        _ => false
        };
    }
}