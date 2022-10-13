using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Simplic.OxS.Data;
using Simplic.OxS.Server.Controller;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simplic.OxS.Server
{
    public class SampleController : OxSController
    {
        private readonly IMapper mapper;

        [HttpPatch("{id}")]
        public async Task<IActionResult> Patch([Required] Guid id)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            string json = @"{ ""name"": ""mustermann""  }";

            // Listen:
            // ++ Wenn Liste im Json = null --> Keine Veränderung 
            // ++ Wenn ein Eintrag Id == default in einer Liste ist --> Neuer EIntrag und Id setzen
            // ++ Wenn Eintrag in Liste und Id ist vorhanden --> Patch
            // Wenn Eintrag in Liste und Id ist nicht vorhanden --> bad request
            // Wenn ein Eintrag "Hart" gelöscht werden soll, muss _remove: true übergeben werden

            // ---
            // Alle Werte die im Json übergeben werden, sollen Teil des Patch sein

            return Ok();
        }

    }

    public class ValidationRequest
    {
        public string Property { get; set; }
        public string Path { get; set; }
        public object Value { get; set; }
    }
}
