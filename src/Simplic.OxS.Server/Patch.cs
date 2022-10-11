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
    public class PatchSampleRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }
    }

    public class SampleData : IDocument<Guid>
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class SampleController : OxSController
    {
        private readonly IMapper mapper;

        [HttpPatch("{id}")]
        public async Task<IActionResult> Patch([Required] Guid id, [Required][FromBody] PatchSampleRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            SampleData originalData = null; // service.Get()
            SampleData patch = mapper.Map<SampleData>(model);

            string json = @"{ ""name"": ""mustermann""  }";

            // Listen:
            // Wenn Liste im Json = null --> Keine Veränderung
            // Wenn ein Eintrag Id == default in einer Liste ist --> Neuer EIntrag und Id setzen
            // Wenn Eintrag in Liste und Id ist vorhanden --> Patch
            // Wenn Eintrag in Liste und Id ist nicht vorhanden --> bad request
            // Wenn ein Eintrag "Hart" gelöscht werden soll, muss _remove: true übergeben werden

            // ---
            // Alle Werte die im Json übergeben werden, sollen Teil des Patch sein

            originalData = PatchHelper.CreatePatch<SampleData, Guid>(originalData, patch, json, (validation) => 
            {
                return true;
            });

            return Ok();
        }

    }

    public interface IItemId<T>
    {
        public T Id { get; set; }
    }

    public static class PatchHelperi
    {
        public static T CreatePatch<T, I>(T originalDocument, T patch, string json, Func<ValidationRequest, bool> validation) where T : IDocument<I>
        {
            // 
            var patchJsonObject = JsonObject.Parse(json);
            var patchObject = patchJsonObject.Deserialize<T>();

            // Filter for object id
            // var filter = Builders<BsonDocument>.Filter.Eq("_id", model.Id);
            // 
            // UpdateDefinition<BsonDocument> update = null;
            // foreach (var change in changesDocument)
            // {
            //     if (update == null)
            //     {
            //         var builder = Builders<BsonDocument>.Update;
            //         update = builder.Set("", change.Value);
            //     }
            //     else
            //     {
            //         update = update.Set("", change.Value);
            //     }
            // }

            // Find values that needs to be patched

            return default;
        }
    }

    public class ValidationRequest
    {
        public string Property { get; set; }
        public string Path { get; set; }
        public object Value { get; set; }
    }
}
