using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test
{
    /// <summary>
    /// Represents a employee.
    /// </summary>
    public class Employee : OrganizationDocumentBase, IDocumentDataExtension
    {
        /// <summary>
        /// Gets or sets the address.
        /// </summary>
        public Address Address { get; set; } = new Address();

        /// <summary>
        /// Gets or sets the PlaceOfBirth.
        /// </summary>
        public string? PlaceOfBirth { get; set; }

        /// <summary>
        /// Gets or sets the Religion.
        /// </summary>
        public string? Religion { get; set; }

        /// <summary>
        /// Gets or sets the amount of Children.
        /// </summary>
        public double? Children { get; set; }

        /// <summary>
        /// Gets or sets the Citizenship.
        /// </summary>
        public string? Citizenship { get; set; }

        /// <summary>
        /// Gets or sets whether SeverelyDisabled or not.
        /// </summary>
        public bool SeverelyDisabled { get; set; }

        /// <summary>
        /// Gets or set the DisabilityLevel.
        /// </summary>
        public int? DisabilityLevel { get; set; }

        /// <summary>
        /// Gets or sets the TaxOffice.
        /// </summary>
        public string? TaxOffice { get; set; }

        /// <summary>
        /// Gets or sets the SocialSecurtiyNumber.
        /// </summary>
        public string? SocialSecurityNumber { get; set; }

        /// <summary>
        /// Gets or sets the HealthInsurance.
        /// </summary>
        public string? HealthInsurance { get; set; }

        /// <summary>
        /// Gets or sets the IdentitycardNumber
        /// </summary>
        public string? IdentityCardNumber { get; set; }

        /// <summary>
        /// Gets or sets the TaxIdentificationNumber.
        /// </summary>
        public string? TaxIdentificationNumber { get; set; }

        /// <summary>
        /// Gets or sets the HealthInsuranceNumber.
        /// </summary>
        public string? HealthInsuranceNumber { get; set; }

        /// <summary>
        /// Gets or sets the HandicappedIdNumber.
        /// </summary>
        public string? HandicappedIdNumber { get; set; }

        /// <summary>
        /// Gets or sets the IssuingAuthority.
        /// </summary>
        public string? IssuingAuthority { get; set; }

        /// <summary>
        /// Gets or sets ValidUntil.
        /// </summary>
        public DateTime? ValidUntil { get; set; }

        /// <summary>
        /// Gets or sets Employment.
        /// </summary>
        public Employment Employment { get; set; } = new Employment();

        /// <summary>
        /// Gets or sets the date and time the employee is created.
        /// </summary>
        public DateTime CreateDateTime { get; set; }

        /// <summary>
        /// Gets or sets the id of the user that created the employee.
        /// </summary>
        public Guid? CreateUserId { get; set; }

        /// <summary>
        /// Gets or sets the name of the user taht created the employee.
        /// </summary>
        public string CreateUserName { get; set; }

        /// <summary>
        /// Gets or sets the date and time the employee is updated.
        /// </summary>
        public DateTime UpdateDateTime { get; set; }

        /// <summary>
        /// Gets or sets the id of the user taht updated the employee.
        /// </summary>
        public Guid? UpdateUserId { get; set; }

        /// <summary>
        /// Gets or sets the name of the user that updated the employee.
        /// </summary>
        public string UpdateUserName { get; set; }
    }
}