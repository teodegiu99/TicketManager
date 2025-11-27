using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.DirectoryServices.AccountManagement;
using TicketAPI.Data;
using TicketAPI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.Linq;

namespace TicketAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly IWebHostEnvironment _env;

        public TicketsController(ApiDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // DTO per ricevere i dati dal form
        public class TicketRequest
        {
            [FromForm(Name = "ProblemType")]
            public string ProblemType { get; set; }

            [FromForm(Name = "Urgency")]
            public string Urgency { get; set; }

            [FromForm(Name = "Funzione")]
            public string? Funzione { get; set; }

            [FromForm(Name = "Macchina")]
            public string Macchina { get; set; }

            [FromForm(Name = "Sede")]
            public string Sede { get; set; }

            [FromForm(Name = "Title")]
            public string Title { get; set; }

            [FromForm(Name = "Message")]
            public string Message { get; set; }

            [FromForm(Name = "Screenshot")]
            public IFormFile? Screenshot { get; set; }
        }

        // DTO per l'aggiornamento (PUT)
        public class TicketUpdateRequest
        {
            public int? StatoId { get; set; }
            public int? AssegnatoaId { get; set; }
            public int? UrgenzaId { get; set; }
            public int? TipologiaId { get; set; }
            public string? Note { get; set; }
        }

        // --- GET ALL OPEN TICKETS ---
        // --- GET TICKETS CON FILTRI ---
        [HttpGet("all")]
        public async Task<IActionResult> GetTickets(
            [FromQuery] string? search,
            [FromQuery] int? assegnatoa_id,
            [FromQuery] int? tipologia_id,
            [FromQuery] int? urgenza_id,
            [FromQuery] int? stato_id,
            [FromQuery] string? sede,
            [FromQuery] string? macchina,
            [FromQuery] string? username
        )
        {
            var query = _context.Ticket
                .Include(t => t.Tipologia)
                .Include(t => t.Urgenza)
                .Include(t => t.Sede)
                .Include(t => t.Stato)
                .Include(t => t.Assegnatoa)
                .AsQueryable();

            // 1. Ricerca Testuale
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.ToLower();
                query = query.Where(t =>
                    t.Titolo.ToLower().Contains(s) ||
                    t.Testo.ToLower().Contains(s) ||
                    (t.Note != null && t.Note.ToLower().Contains(s))
                );
            }
            else
            {
                // LOGICA DEFAULT: Se NON sto cercando e NON filtro per stato...
                if (!stato_id.HasValue)
                {
                    // ...mostra solo quelli NON terminati (Aperti/In Lavorazione)
                    query = query.Where(t => t.StatoId != 3);
                }
            }

            // 2. Filtri specifici
            if (assegnatoa_id.HasValue) query = query.Where(t => t.AssegnatoaId == assegnatoa_id.Value);
            if (tipologia_id.HasValue) query = query.Where(t => t.TipologiaId == tipologia_id.Value);
            if (urgenza_id.HasValue) query = query.Where(t => t.UrgenzaId == urgenza_id.Value);

            // Se specifico uno stato (es. "Terminato"), il filtro default sopra viene ignorato e uso questo
            if (stato_id.HasValue) query = query.Where(t => t.StatoId == stato_id.Value);

            if (!string.IsNullOrEmpty(sede)) query = query.Where(t => t.Sede != null && t.Sede.Nome == sede);
            if (!string.IsNullOrEmpty(macchina)) query = query.Where(t => t.Macchina != null && t.Macchina.ToLower().Contains(macchina.ToLower()));
            if (!string.IsNullOrEmpty(username)) query = query.Where(t => t.Username.ToLower().Contains(username.ToLower()));

            // Esecuzione query
            var tickets = await query
                .OrderByDescending(t => t.UrgenzaId)
                .ThenByDescending(t => t.DataCreazione)
                .Select(t => new
                {
                    Id = t.Nticket,
                    Nticket = t.Nticket,
                    Titolo = t.Titolo,
                    Testo = t.Testo,
                    TipologiaNome = t.Tipologia != null ? t.Tipologia.Nome : "N/D",
                    UrgenzaNome = t.Urgenza != null ? t.Urgenza.Nome : "N/D",
                    SedeNome = t.Sede != null ? t.Sede.Nome : "N/D",
                    StatoNome = t.Stato != null ? t.Stato.Nome : "N/D",
                    TipologiaColore = t.Tipologia != null ? t.Tipologia.Colore : null,
                    Username = t.Username,
                    Funzione = t.Funzione,
                    Macchina = t.Macchina,
                    AssegnatoaNome = t.Assegnatoa != null ? t.Assegnatoa.UsernameAd : "Non assegnato",
                    DataCreazione = t.DataCreazione,
                    // INCLUDI LA DATA CHIUSURA
                    DataChiusura = t.DataChiusura,
                    ScreenshotPath = t.ScreenshotPath,
                    StatoId = t.StatoId,
                    AssegnatoaId = t.AssegnatoaId,
                    TipologiaId = t.TipologiaId,
                    UrgenzaId = t.UrgenzaId,
                    Note = t.Note
                })
                .ToListAsync();

            return Ok(tickets);
        }

        // --- UPDATE TICKET ---
        [HttpPut("{nticket}/update")]
        public async Task<IActionResult> UpdateTicket(int nticket, [FromBody] TicketUpdateRequest request)
        {
            var ticket = await _context.Ticket.FirstOrDefaultAsync(t => t.Nticket == nticket);
            if (ticket == null) return NotFound($"Ticket {nticket} non trovato.");

            bool modified = false;

            // --- LOGICA CAMBIO STATO E DATA CHIUSURA ---
            if (request.StatoId.HasValue && ticket.StatoId != request.StatoId.Value)
            {
                ticket.StatoId = request.StatoId.Value;

                // 3 è l'ID per "Terminato"
                if (ticket.StatoId == 3)
                {
                    // CORREZIONE: Usa UtcNow invece di Now per compatibilità con Postgres
                    ticket.DataChiusura = DateTime.UtcNow;
                }
                else
                {
                    // Se cambia in qualsiasi altro stato -> Null
                    ticket.DataChiusura = null;
                }

                modified = true;
            }

            // ... (Il resto del metodo rimane uguale: controlli su Note, AssegnatoaId, ecc.)

            if (request.Note != null)
            {
                if (ticket.Note != request.Note)
                {
                    ticket.Note = request.Note;
                    modified = true;
                }
            }

            if (request.AssegnatoaId.HasValue || request.AssegnatoaId == null)
            {
                int? idDaSalvare = request.AssegnatoaId == 0 ? null : request.AssegnatoaId;
                if (ticket.AssegnatoaId != idDaSalvare)
                {
                    ticket.AssegnatoaId = idDaSalvare;
                    modified = true;
                }
            }

            if (request.UrgenzaId.HasValue && ticket.UrgenzaId != request.UrgenzaId.Value)
            {
                ticket.UrgenzaId = request.UrgenzaId.Value;
                modified = true;
            }

            if (request.TipologiaId.HasValue && ticket.TipologiaId != request.TipologiaId.Value)
            {
                ticket.TipologiaId = request.TipologiaId.Value;
                modified = true;
            }

            if (modified) await _context.SaveChangesAsync();

            return Ok();
        }

        // --- CREATE TICKET ---
        [HttpPost]
        public async Task<IActionResult> CreateTicket([FromForm] TicketRequest request)
        {
            string adUsername = User.Identity.Name;
            string? userDisplayName = adUsername;

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    var userPrincipal = UserPrincipal.FindByIdentity(context, adUsername);
                    if (userPrincipal != null) userDisplayName = userPrincipal.DisplayName ?? adUsername;
                }
            }
            catch (Exception) { }

            var urgenza = await _context.Urgenza.FirstOrDefaultAsync(u => u.Nome == request.Urgency);
            var tipologia = await _context.Tipologie.FirstOrDefaultAsync(t => t.Nome == request.ProblemType);
            var sede = await _context.Sedi.FirstOrDefaultAsync(s => s.Nome == request.Sede);

            if (urgenza == null || tipologia == null || sede == null) return BadRequest("Dati non validi.");

            string? screenshotDbPath = null;
            if (request.Screenshot != null && request.Screenshot.Length > 0)
            {
                var fileName = $"{Guid.NewGuid()}_{request.Screenshot.FileName}";
                var filePath = Path.Combine(_env.ContentRootPath, "Uploads", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.Screenshot.CopyToAsync(stream);
                }
                screenshotDbPath = Path.Combine("Uploads", fileName);
            }

            var newTicket = new Ticket
            {
                Username = userDisplayName ?? "Sconosciuto",
                Funzione = request.Funzione,
                Titolo = request.Title,
                Testo = request.Message,
                ScreenshotPath = screenshotDbPath,
                DataCreazione = DateTime.UtcNow,
                Macchina = request.Macchina,
                TipologiaId = tipologia.Id,
                UrgenzaId = urgenza.Id,
                SedeId = sede.Id
            };

            _context.Ticket.Add(newTicket);
            await _context.SaveChangesAsync();

            return Ok(newTicket);
        }

        // --- DROPDOWNS ---

        [HttpGet("tipologie")]
        public async Task<IActionResult> GetTipologie()
        {
            // CORREZIONE: Seleziona oggetto completo { Id, Nome }
            var data = await _context.Tipologie
                .Select(t => new { t.Id, t.Nome, t.Colore })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("urgenze")]
        public async Task<IActionResult> GetUrgenze()
        {
            // CORREZIONE: Seleziona oggetto completo { Id, Nome }
            var data = await _context.Urgenza
                .Select(u => new { u.Id, u.Nome })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("sedi")]
        public async Task<IActionResult> GetSedi()
        {
            var data = await _context.Sedi.Select(s => s.Nome).ToListAsync();
            return Ok(data);
        }

        [HttpGet("stati")]
        public async Task<IActionResult> GetAllStati()
        {
            var stati = await _context.Stati
                .OrderBy(s => s.Id)
                .Select(s => new { s.Id, s.Nome })
                .ToListAsync();
            return Ok(stati);
        }
    }
}