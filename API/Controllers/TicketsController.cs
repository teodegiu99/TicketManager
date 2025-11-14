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
using System.Linq; // Aggiunto per .Select

namespace TicketAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly IWebHostEnvironment _env;

        // Inietta il "ponte" del DB e l'ambiente per i file
        public TicketsController(ApiDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // --- Modello per ricevere i dati dal form ---
        // (Riflette i campi inviati da ClientUser)
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

        // --- Endpoint per recuperare TUTTI i ticket (per ClientIT) ---
        [HttpGet("all")]
        public async Task<IActionResult> GetAllTickets([FromQuery] int? assegnatoaId)
        {
            // Inizia la query sulla tabella Ticket
            var query = _context.Ticket
                .Include(t => t.Tipologia)
                .Include(t => t.Urgenza)
                .Include(t => t.Sede)
                .Include(t => t.Stato) // Include il nuovo stato
                .Include(t => t.Assegnatoa)
                .AsQueryable(); // Permette di aggiungere filtri

            // --- Logica di Filtro ---
            // Se l'URL (ClientIT) passa un nome utente (es. ?assegnatoaId=5)
            if (assegnatoaId.HasValue)
            {
                // Filtra per ID (int)
                query = query.Where(t => t.AssegnatoaId == assegnatoaId.Value);
            }
            // Se 'assegnatoaId' è null o vuoto, il filtro non viene applicato
            // e verranno restituiti tutti i ticket (come richiesto da "Mostra Tutti")

            // Esegui la query finale
            var tickets = await query
                .OrderByDescending(t => t.DataCreazione) // I più recenti prima
                .Select(t => new
                {
                    // Mappiamo Nticket sia su Id che su Nticket
                    // per corrispondere al ClientIT/Models/TicketViewModel.cs
                    Id = t.Nticket,
                    Nticket = t.Nticket,
                    Titolo = t.Titolo,
                    Testo = t.Testo,

                    // Usiamo le proprietà di navigazione (rese sicure per i null)
                    TipologiaNome = t.Tipologia != null ? t.Tipologia.Nome : "N/D",
                    UrgenzaNome = t.Urgenza != null ? t.Urgenza.Nome : "N/D",
                    SedeNome = t.Sede != null ? t.Sede.Nome : "N/D",
                    StatoNome = t.Stato != null ? t.Stato.Nome : "N/D", // Aggiunto

                    Username = t.Username,
                    Funzione = t.Funzione,
                    Macchina = t.Macchina,
                    AssegnatoaNome = t.Assegnatoa != null ? t.Assegnatoa.UsernameAd : "Non assegnato",
                    DataCreazione = t.DataCreazione,
                    ScreenshotPath = t.ScreenshotPath
                })
                .ToListAsync();

            return Ok(tickets);
        }


        // --- Logica POST per creare un ticket (per ClientUser) ---
        [HttpPost]
        public async Task<IActionResult> CreateTicket([FromForm] TicketRequest request)
        {
            // --- 1. Trova l'utente AD ---
            string adUsername = User.Identity.Name;
            string? userDisplayName = adUsername;

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    var userPrincipal = UserPrincipal.FindByIdentity(context, adUsername);
                    if (userPrincipal != null)
                    {
                        userDisplayName = userPrincipal.DisplayName ?? adUsername;
                    }
                }
            }
            catch (Exception) { /* Usa l'username come fallback */ }

            // --- 2. Trova gli ID delle chiavi esterne ---
            var urgenza = await _context.Urgenza // Usa Urgenze (plurale) come da DbContext
                .FirstOrDefaultAsync(u => u.Nome == request.Urgency);

            var tipologia = await _context.Tipologie
                .FirstOrDefaultAsync(t => t.Nome == request.ProblemType);

            var sede = await _context.Sedi
                .FirstOrDefaultAsync(s => s.Nome == request.Sede);

            if (urgenza == null || tipologia == null || sede == null)
            {
                return BadRequest("Uno dei valori (urgenza, tipo, sede) non è valido.");
            }

            // --- 3. Gestisci l'upload dello Screenshot ---
            string? screenshotDbPath = null;
            if (request.Screenshot != null && request.Screenshot.Length > 0)
            {
                try
                {
                    var fileName = $"{Guid.NewGuid()}_{request.Screenshot.FileName}";
                    var filePath = Path.Combine(_env.ContentRootPath, "Uploads", fileName);

                    // Assicura che la cartella "Uploads" esista
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.Screenshot.CopyToAsync(stream);
                    }
                    // Salva il percorso relativo (es. "Uploads/nomefile.png")
                    screenshotDbPath = Path.Combine("Uploads", fileName);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Errore durante il salvataggio del file: {ex.Message}");
                }
            }

            // --- 4. Crea e Salva il Ticket ---
            var newTicket = new Ticket
            {
                // Nticket NON viene impostato qui.
                // [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
                // nel Modello Ticket.cs dice a EF Core di 
                // lasciare che sia PostgreSQL a generarlo.

                Username = userDisplayName ?? "Utente Sconosciuto",
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

            try
            {
                _context.Ticket.Add(newTicket); // Usa Ticket (singolare) come da DbContext
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Mostra l'errore interno per il debug
                return StatusCode(500, $"Errore durante il salvataggio sul DB: {ex.InnerException?.Message ?? ex.Message}");
            }

            // Fatto!
            return Ok(newTicket); // Restituisce il ticket appena creato (con Nticket)
        }

        // --- Endpoint per i dropdown (per ClientUser) ---
        [HttpGet("tipologie")]
        public async Task<IActionResult> GetTipologie()
        {
            var data = await _context.Tipologie
                .Select(t => t.Nome)
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("urgenze")]
        public async Task<IActionResult> GetUrgenze()
        {
            var data = await _context.Urgenza // Usa Urgenze (plurale) come da DbContext
                .Select(u => u.Nome)
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("sedi")]
        public async Task<IActionResult> GetSedi()
        {
            var data = await _context.Sedi
                .Select(s => s.Nome)
                .ToListAsync();
            return Ok(data);
        }
    }
}