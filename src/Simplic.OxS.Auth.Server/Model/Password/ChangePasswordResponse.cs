using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Auth.Model
{
    public class ChangePasswordResponse
    {
        /// <summary>
        /// Gets or sets the token to verify
        /// </summary>
        public Guid TokenId { get; set; }
    }
}
