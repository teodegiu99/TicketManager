using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.DirectoryServices.AccountManagement;
using TicketAPI.Data;     // Il nostro ponte DB
using TicketAPI.Models;   // I nostri modelli
using Microsoft.EntityFrameworkCore; // Per le query

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
        // (La tua classe TicketRequest rimane invariata)
        public class TicketRequest
        {
            [FromForm(Name = "ProblemType")]
            public string ProblemType { get; set; }

            [FromForm(Name = "Urgency")]
            public string Urgency { get; set; }

            [FromForm(Name = "Funzione")]
            public string Funzione { get; set; }

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


        // --- Logica POST (Rimane invariata) ---
        [HttpPost]
        public async Task<IActionResult> CreateTicket([FromForm] TicketRequest request)
        {
            // ... (tutta la tua logica di salvataggio rimane qui)
            // ...
            // (codice omesso per brevità)
            // ...

            // --- 1. Trova l'utente AD ---
            string adUsername = User.Identity.Name;
            string? userDisplayName = adUsername; // Default

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
            catch (Exception) { /* Pazienza, usiamo l'username */ }

            // --- 2. Trova gli ID delle chiavi esterne ---
            var urgenza = await _context.Urgenza
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
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.Screenshot.CopyToAsync(stream);
                    }
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
                Username = userDisplayName,
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
                _context.Ticket.Add(newTicket);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore durante il salvataggio sul DB: {ex.Message}");
            }

            return Ok(newTicket);
        }

        // --- =================================== ---
        // --- NUOVI ENDPOINT PER POPOLARE I DROPDOWN ---
        // --- =================================== ---

        /// <summary>
        /// Restituisce la lista dei nomi delle Tipologie
        /// </summary>
        [HttpGet("tipologie")]
        public async Task<IActionResult> GetTipologie()
        {
            var data = await _context.Tipologie
                                 .Select(t => t.Nome) // Seleziona solo la colonna "nome"
                                 .ToListAsync();
            return Ok(data);
        }

        /// <summary>
        /// Restituisce la lista dei nomi delle Urgenze
        /// </summary>
        [HttpGet("urgenze")]
        public async Task<IActionResult> GetUrgenze()
        {
            var data = await _context.Urgenza
                                 .Select(u => u.Nome)
                                 .ToListAsync();
            return Ok(data);
        }

        /// <summary>
        /// Restituisce la lista dei nomi delle Sedi
        /// </summary>
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