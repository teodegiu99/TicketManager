using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;

namespace TicketAPI.Controllers
{
    // Solo utenti autenticati possono accedere (potresti voler aggiungere policy per soli admin)
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [SupportedOSPlatform("windows")] // Indica che funziona solo su Windows (per AD)
    public class AdminController : ControllerBase
    {
        public class AdminUserDto
        {
            public string Username { get; set; }
            public string DisplayName { get; set; }
            public string Email { get; set; }
            public bool IsLocked { get; set; }
            public bool IsDisabled { get; set; }
            public bool PasswordNeverExpires { get; set; }
            public DateTime? PasswordExpirationDate { get; set; }
            public DateTime? LastPasswordSet { get; set; }
            public string Message { get; set; } // Per eventuali errori
        }

        public class PasswordResetRequest
        {
            public string Username { get; set; }
            public string NewPassword { get; set; }
            public bool UserMustChangePassword { get; set; }
        }

        [HttpGet("user/{username}")]
        public IActionResult GetUserInfo(string username)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    var user = UserPrincipal.FindByIdentity(context, username);
                    if (user == null) return NotFound("Utente non trovato in Active Directory.");

                    var dto = new AdminUserDto
                    {
                        Username = user.SamAccountName,
                        DisplayName = user.DisplayName,
                        Email = user.EmailAddress,
                        IsLocked = user.IsAccountLockedOut(),
                        IsDisabled = user.Enabled == false, // Enabled è nullable
                        LastPasswordSet = user.LastPasswordSet,
                        PasswordNeverExpires = user.PasswordNeverExpires
                    };

                    // Calcolo scadenza password (logica semplificata)
                    // In AD reale bisognerebbe controllare le policy di dominio, 
                    // ma UserPrincipal non espone direttamente la data di scadenza calcolata.
                    // Possiamo provare a recuperarla dalle proprietà estese se necessario, 
                    // o lasciare null se "PasswordNeverExpires" è true.

                    return Ok(dto);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore AD: {ex.Message}");
            }
        }

        [HttpPost("unlock")]
        public IActionResult UnlockUser([FromBody] string username)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    var user = UserPrincipal.FindByIdentity(context, username);
                    if (user == null) return NotFound("Utente non trovato.");

                    if (user.IsAccountLockedOut())
                    {
                        user.UnlockAccount();
                        user.Save();
                        return Ok("Account sbloccato con successo.");
                    }
                    return Ok("L'account non era bloccato.");
                }
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPost("toggle-enable")]
        public IActionResult ToggleEnable([FromBody] string username)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    var user = UserPrincipal.FindByIdentity(context, username);
                    if (user == null) return NotFound("Utente non trovato.");

                    // Inverte lo stato
                    bool isCurrentlyEnabled = user.Enabled ?? false;
                    user.Enabled = !isCurrentlyEnabled;
                    user.Save();

                    return Ok(new { IsDisabled = !user.Enabled, Message = user.Enabled == true ? "Account Abilitato" : "Account Disabilitato" });
                }
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] PasswordResetRequest request)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    var user = UserPrincipal.FindByIdentity(context, request.Username);
                    if (user == null) return NotFound("Utente non trovato.");

                    user.SetPassword(request.NewPassword);

                    if (request.UserMustChangePassword)
                    {
                        user.ExpirePasswordNow();
                    }

                    user.Save();
                    return Ok("Password reimpostata correttamente.");
                }
            }
            catch (Exception ex) { return StatusCode(500, $"Errore Reset Password: {ex.Message}"); }
        }
    }
}