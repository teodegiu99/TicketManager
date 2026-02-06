using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Data;
using TicketAPI.Models;
using TicketAPI.Models.DTO; // Assicurati di avere il namespace corretto

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentazioneController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public DocumentazioneController(ApiDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentazioneDto>>> GetDocumentazione()
        {
            // 1. Scarica i documenti
            var docs = await _context.Documentazione.ToListAsync();

            // 2. Scarica le Tipologie esistenti (usate come Categorie)
            // Creiamo un dizionario per accesso veloce: ID -> Oggetto Tipologia
            var tipologieDict = await _context.Tipologie
                .ToDictionaryAsync(t => t.Id);

            // 3. Scarica le Keywords
            var keywordsDict = await _context.Keywords
                .ToDictionaryAsync(k => k.Id, k => k.Nome);

            // 4. Unisci i dati
            var result = docs.Select(d =>
            {
                // Cerchiamo la tipologia corrispondente
                var tipologia = tipologieDict.ContainsKey(d.Categoria)
                    ? tipologieDict[d.Categoria]
                    : null;

                return new DocumentazioneDto
                {
                    Id = d.Id,
                    Nticket = d.Nticket,
                    Titolo = d.Titolo,
                    Soluzione = d.Soluzione,
                    Query = d.Query, // <--- Aggiungi questo
                    // Mappatura Tipologia
                    CategoriaId = d.Categoria,
                    CategoriaNome = tipologia?.Nome ?? "Non specificata",
                    CategoriaColore = tipologia?.Colore ?? "#CCCCCC", // Grigio default

                    // Mappatura Keywords
                    KeywordIds = d.Keywords,
                    KeywordNomi = d.Keywords != null
                        ? d.Keywords
                            .Where(kId => keywordsDict.ContainsKey(kId))
                            .Select(kId => keywordsDict[kId])
                            .ToList()
                        : new List<string>()
                };
            }).ToList();

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<Documentazione>> PostDocumentazione([FromBody] CreateDocRequest request)
        {
            if (request == null) return BadRequest();

            // 1. Gestione Keywords: Trasformiamo le stringhe in ID (creandole se non esistono)
            var keywordIds = new List<int>();

            if (request.Keywords != null && request.Keywords.Any())
            {
                foreach (var kName in request.Keywords)
                {
                    var cleanName = kName.Trim();
                    if (string.IsNullOrWhiteSpace(cleanName)) continue;

                    // Cerca se esiste già (Case Insensitive)
                    var existingKey = await _context.Keywords
                        .FirstOrDefaultAsync(k => k.Nome.ToLower() == cleanName.ToLower());

                    if (existingKey != null)
                    {
                        keywordIds.Add(existingKey.Id);
                    }
                    else
                    {
                        // Crea nuova keyword
                        var newKey = new Keyword { Nome = cleanName };
                        _context.Keywords.Add(newKey);
                        await _context.SaveChangesAsync(); // Salva subito per avere l'ID
                        keywordIds.Add(newKey.Id);
                    }
                }
            }

            // 2. Crea l'oggetto Documentazione
            var doc = new Documentazione
            {
                Nticket = request.Nticket,
                Titolo = request.Titolo,
                Soluzione = request.Soluzione,
                Query = request.Query,
                Categoria = request.CategoriaId,
                Keywords = keywordIds.ToArray() // Array di int per Postgres
            };

            _context.Documentazione.Add(doc);
            await _context.SaveChangesAsync();

            return Ok(doc);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDocumentazione(int id, [FromBody] DocumentazioneDto dto)
        {
            var doc = await _context.Documentazione.FindAsync(id);
            if (doc == null) return NotFound();

            doc.Titolo = dto.Titolo;
            doc.Soluzione = dto.Soluzione;

            // CORREZIONE QUI: Usa .Categoria (nome nel DB) = dto.CategoriaId (nome nel frontend)
            doc.Categoria = dto.CategoriaId;

            // Gestione Query se presente
            doc.Query = dto.Query;

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}