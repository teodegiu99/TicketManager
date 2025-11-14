using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Data;
using TicketAPI.Models;

namespace TicketAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public AuthController(ApiDbContext context)
        {
            _context = context;
        }

        [HttpGet("check")]
        public async Task<IActionResult> CheckUserPermission()
        {
            string adUsername = User.Identity.Name;

            if (string.IsNullOrEmpty(adUsername))
            {
                return Unauthorized("Autenticazione AD fallita.");
            }

            var utenteAbilitato = await _context.ItUtenti
                .FirstOrDefaultAsync(u => u.UsernameAd == adUsername);

            if (utenteAbilitato == null)
            {
                // --- QUESTA È LA CORREZIONE ---
                // Se non è nella tabella, restituiamo un errore 403 (Forbidden)
                // con un messaggio chiaro, invece di usare Forbid("messaggio")
                // che causa il crash.
                return StatusCode(403, "Utente non abilitato all'accesso.");
                // --- FINE DELLA CORREZIONE ---
            }

            return Ok(utenteAbilitato);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetItUsers()
        {
            var utenti = await _context.ItUtenti
                .Select(u => new
                {
                    u.Id,
                    u.UsernameAd,
                    u.Permesso,
                    u.TipologieAbilitate
                })
                .ToListAsync();

            return Ok(utenti);
        }
    }
}