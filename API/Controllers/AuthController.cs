using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Data;

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
        /// </summary>
        [HttpGet("check")]
        public async Task<IActionResult> CheckUserPermission()
        {
            // Ottiene l'username (es. "AZIENDA\mrossi") dall'autenticazione Windows
            string adUsername = User.Identity.Name;

            if (string.IsNullOrEmpty(adUsername))
            {
                return Unauthorized("Autenticazione AD fallita.");
            }

            // Cerca l'utente nella nostra tabella 'it_utenti'
            var utenteAbilitato = await _context.ItUtenti
                .FirstOrDefaultAsync(u => u.UsernameAd == adUsername);

            if (utenteAbilitato == null)
            {
                // Se non è nella tabella, non è autorizzato
                return Forbid("Utente non abilitato all'accesso.");
            }

            // Se l'utente esiste, restituiamo i suoi dati (ID, Permesso, Tipologie)
            // L'app client userà questi dati per decidere cosa mostrare.
            return Ok(utenteAbilitato);
        }
    }
}