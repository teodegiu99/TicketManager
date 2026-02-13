using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.DirectoryServices.AccountManagement; // Necessario per Active Directory
using TicketAPI.Data;

namespace TicketAPI.Controllers
{
    [Authorize] // Richiede autenticazione Windows/AD
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
        /// Controlla se l'utente AD autenticato è presente nella tabella 'it_utenti' 
        /// e ne restituisce i permessi.
        /// </summary>
        [HttpGet("check")]
        public async Task<IActionResult> CheckUserPermission()
        {
            string? adUsername = User.Identity?.Name;

            if (string.IsNullOrEmpty(adUsername))
            {
                return Unauthorized("Autenticazione AD fallita.");
            }

            var utenteAbilitato = await _context.ItUtenti
                .FirstOrDefaultAsync(u => u.UsernameAd == adUsername);

            if (utenteAbilitato == null)
            {
                return StatusCode(403, "Utente non abilitato all'accesso IT.");
            }

            return Ok(utenteAbilitato);
        }

        /// <summary>
        /// Restituisce la lista degli utenti IT (per dropdown e filtri nel ClientIT).
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetItUsers()
        {
            var utenti = await _context.ItUtenti
                .OrderBy(u => u.UsernameAd)
                .Select(u => new
                {
                    u.Id,
                    u.UsernameAd,
                    u.Permesso,
                    u.NomeCompleto,
                    Nome = !string.IsNullOrEmpty(u.NomeCompleto) ? u.NomeCompleto : (!string.IsNullOrEmpty(u.Nome) ? u.Nome : u.UsernameAd)
                })
                .ToListAsync();

            return Ok(utenti);
        }

        /// <summary>
        /// Restituisce una lista di tutti gli utenti del dominio (filtrata)
        /// per popolare l'AutoSuggestBox "Per Conto Di" nel ClientUser.
        /// </summary>
        [HttpGet("ad-users-list")]
        public IActionResult GetAdUsersList()
        {
            var users = new List<string>();

            // 1. LISTA PREFISSI DA ESCLUDERE (Blacklist)
            var excludedPrefixes = new[]
            {
        "help", "admin", "microsoft", "protex",
        "health", "dosch", "assistenza"
    };

            // 2. LISTA OU DA INCLUDERE (Whitelist)
            // Cerchiamo queste stringhe nel percorso dell'utente
            var allowedOus = new[]
            {
        "OU=Zegna Baruffa",
        "OU=Zegna Baruffa_Botto Poala"
    };

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                using (var searcher = new PrincipalSearcher(new UserPrincipal(context)))
                {
                    foreach (var result in searcher.FindAll())
                    {
                        if (result is UserPrincipal user)
                        {
                            // Recuperiamo il Nome Visualizzato
                            string displayName = !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : user.Name;

                            // Recuperiamo il DistinguishedName (percorso completo es: CN=Nome,OU=Reparto,DC=...)
                            string distinguishedName = user.DistinguishedName;

                            if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(distinguishedName))
                            {
                                // A. CONTROLLO OU (Whitelist)
                                // L'utente deve trovarsi in una delle OU specificate
                                bool isInAllowedOu = allowedOus.Any(ou =>
                                    distinguishedName.Contains(ou, StringComparison.OrdinalIgnoreCase));

                                if (isInAllowedOu)
                                {
                                    // B. CONTROLLO PREFISSI (Blacklist)
                                    // Il nome non deve iniziare con i prefissi esclusi
                                    bool isExcluded = excludedPrefixes.Any(prefix =>
                                        displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                                    if (!isExcluded)
                                    {
                                        users.Add(displayName);
                                    }
                                }
                            }
                        }
                    }
                }

                return Ok(users.OrderBy(u => u).Distinct().ToList());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore nel recupero utenti AD: {ex.Message}");
                return Ok(new List<string>());
            }
        }
    }
}