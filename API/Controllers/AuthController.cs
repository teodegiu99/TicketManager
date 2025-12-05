using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using TicketAPI.Data; // Assicurati che questo using ci sia

namespace TicketAPI.Controllers
{
    [Authorize] // Richiede che l'utente sia autenticato (con AD)
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public AuthController(ApiDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Controlla se l'utente AD autenticato è presente
        /// nella tabella 'it_utenti' e ne restituisce i permessi.
        /// Chiamato da ClientIT all'avvio.
        /// </summary>
        [HttpGet("check")]
        public async Task<IActionResult> CheckUserPermission()
        {
            // Ottiene l'username (es. "AZIENDA\mrossi") dall'autenticazione Windows
            string? adUsername = User.Identity?.Name;

            if (string.IsNullOrEmpty(adUsername))
            {
                return Unauthorized("Autenticazione AD fallita.");
            }

            // Cerca l'utente nella nostra tabella 'it_utenti'
            var utenteAbilitato = await _context.ItUtenti
                .FirstOrDefaultAsync(u => u.UsernameAd == adUsername);

            if (utenteAbilitato == null)
            {
                // Se non è nella tabella, non è autorizzato (Errore 403)
                // Usiamo StatusCode per evitare il crash dell'API
                return StatusCode(403, "Utente non abilitato all'accesso.");
            }

            // Se l'utente esiste, restituiamo i suoi dati (ID, Permesso, Tipologie)
            // L'app client userà questi dati per decidere cosa mostrare.
            return Ok(utenteAbilitato);
        }

        /// <summary>
        /// (NUOVO) Endpoint per popolare la lista utenti nel ClientIT
        /// (sia la colonna sinistra sia il dropdown "Assegnato a")
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetItUsers()
        {
            var utenti = await _context.ItUtenti
                .OrderBy(u => u.UsernameAd)
                // Selezioniamo solo i campi che servono al client
                .Select(u => new 
                { 
                    u.Id, 
                    u.UsernameAd, 
                    u.Permesso,
                    u.NomeCompleto,
                    Nome = !string.IsNullOrEmpty(u.Nome) ? u.Nome : u.UsernameAd
                    // Non inviamo le tipologie abilitate per ora
                }) 
                .ToListAsync();
            
            return Ok(utenti);
        }
    }
}