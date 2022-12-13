namespace Simplic.OxS.Server.Test
{
    /// <summary>
    /// Represents the base model of a employee.
    /// </summary>
    public class EmployeeBaseModel
    {
        /// <summary>
        /// Gets or sets the address.
        /// </summary>
        public AddressModel? Address { get; set; }

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
        public EmploymentModel? Employment { get; set; }
    }
}