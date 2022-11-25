namespace Simplic.OxS.Server.Test
{
    /// <summary>
    /// Represents the model of an address.
    /// </summary>
    public class AddressModel
    {
        /// <summary>
        /// Gets or sets the first name.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Gets or sets the last name.
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Gets or sets the company name.
        /// </summary>
        public string? CompanyName { get; set; }

        /// <summary>
        /// Gets or sets the first additional data.
        /// </summary>
        public string? Additional01 { get; set; }

        /// <summary>
        /// Gets or sets the second additional data.
        /// </summary>
        public string? Additional02 { get; set; }

        /// <summary>
        /// Gets or sets the street.
        /// </summary>
        public string? Street { get; set; }

        /// <summary>
        /// Gets or sets the house number.
        /// </summary>
        public string? HouseNumber { get; set; }

        /// <summary>
        /// Gets or sets the zipcode.
        /// </summary>
        public string? Zipcode { get; set; }

        /// <summary>
        /// Gets or sets the city.
        /// </summary>
        public string? City { get; set; }

        /// <summary>
        /// Gets or sets the district.
        /// </summary>
        public string? District { get; set; }

        /// <summary>
        /// Gets or sets the federal state of the address.
        /// </summary>
        public string? FederalState { get; set; }

        /// <summary>
        /// Gets or sets the country iso.
        /// </summary>
        public string? CountryIso { get; set; }

        /// <summary>
        /// Gets or sets the country.
        /// </summary>
        public string? Country { get; set; }

        /// <summary>
        /// Gets or sets the latitude.
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Gets or sets the longitude.
        /// </summary>
        public double Longitude { get; set; }
    }
}