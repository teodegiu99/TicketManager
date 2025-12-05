using Microsoft.AspNetCore.Mvc;
using System.DirectoryServices.AccountManagement; // Serve il pacchetto NuGet omonimo
using TicketAPI.Data; // Il tuo namespace del DbContext
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace TicketAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public SyncController(ApiDbContext context)
        {
            _context = context;
        }

        [HttpPost("run")]
        public async Task<IActionResult> SyncAttributiAd()
        {
            // 1. Prendi tutti gli utenti dal DB che hanno uno username
            var utentiDb = await _context.ItUtenti
                                         .Where(u => !string.IsNullOrEmpty(u.Username))
                                         .ToListAsync();

            int contatoreAggiornati = 0;

            // 2. Collegati ad Active Directory
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    foreach (var utente in utentiDb)
                    {
                        // Pulizia username (toglie DOMINIO\ se c'è)
                        string cleanUser = utente.Username.Contains("\\")
                                         ? utente.Username.Split('\\')[1]
                                         : utente.Username;

                        // Cerca l'utente in AD
                        var adUser = UserPrincipal.FindByIdentity(context, cleanUser);

                        if (adUser != null)
                        {
                            // 3. Prendi il DisplayName (es. "Mario Rossi")
                            string nomeDaAd = adUser.DisplayName;

                            // Fallback: Se DisplayName è vuoto, unisci Nome + Cognome
                            if (string.IsNullOrEmpty(nomeDaAd))
                            {
                                nomeDaAd = $"{adUser.GivenName} {adUser.Surname}".Trim();
                            }

                            // 4. Se il DB è diverso da AD, aggiorna la nuova colonna
                            if (utente.NomeCompleto != nomeDaAd)
                            {
                                utente.NomeCompleto = nomeDaAd;
                                contatoreAggiornati++;
                            }
                        }
                    }
                }

                // 5. Salva tutto nel DB in un colpo solo
                if (contatoreAggiornati > 0)
                {
                    await _context.SaveChangesAsync();
                }

                return Ok(new { Message = "Sync completato", UtentiAggiornati = contatoreAggiornati });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore AD: {ex.Message}");
            }
        }
    }
}