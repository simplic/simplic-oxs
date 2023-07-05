using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Server;

/// <summary>
/// When used over a property in a asp.net core model property, its content will be converted
/// to lowercase.
/// </summary>
public class LowercaseAttribute : ValidationAttribute
{
	/// <summary>
	/// Execute validation and turn content into lower-case
	/// </summary>
	/// <param name="value">Input value for validation</param>
	/// <param name="validationContext">Current validation context.</param>
	/// <returns>Always success as validation resul</returns>
	protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
	{
		if (value != null && validationContext.MemberName != null)
		{
			string? lowercaseValue = value.ToString()?.ToLower();

			if(lowercaseValue == null) 
				return ValidationResult.Success;

			validationContext.ObjectType.GetProperty(validationContext.MemberName)?
										.SetValue(validationContext.ObjectInstance, lowercaseValue);
		}

		return ValidationResult.Success;
	}
}
