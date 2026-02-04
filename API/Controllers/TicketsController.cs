using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.DirectoryServices.AccountManagement;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TicketAPI.Data;
using TicketAPI.Models;

namespace TicketAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ApiDbContext _context;
        private readonly IWebHostEnvironment _env;
        public TicketsController(ApiDbContext context, IWebHostEnvironment env, IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
        }

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

            [FromForm(Name = "PerContoDi")]
            public string? PerContoDi { get; set; }

            [FromForm(Name = "Screenshot")]
            public IFormFile? Screenshot { get; set; }
        }

        public class TicketUpdateRequest
        {
            public int? StatoId { get; set; }
            public int? AssegnatoaId { get; set; }
            public int? UrgenzaId { get; set; }
            public int? TipologiaId { get; set; }
            public string? Note { get; set; }
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetTickets(
            [FromQuery] string? search,
            [FromQuery] int? assegnatoa_id,
            [FromQuery] int? tipologia_id,
            [FromQuery] int? urgenza_id,
            [FromQuery] int? stato_id,
            [FromQuery] string? sede,
            [FromQuery] string? macchina,
            [FromQuery] string? username,
            [FromQuery] int? nticket,
            [FromQuery] bool includeAll = false,
            [FromQuery] bool mine = false // <--- NUOVO PARAMETRO
        )
        {
            var query = _context.Ticket
                .Include(t => t.Tipologia)
                .Include(t => t.Urgenza)
                .Include(t => t.Sede)
                .Include(t => t.Stato)
                .Include(t => t.Assegnatoa)
                .AsQueryable();

            // --- LOGICA "I MIEI TICKET" ---
            if (mine)
            {
                // Recupera l'utente corrente AD
                string adUsername = User.Identity.Name;
                string userDisplayName = adUsername; // Fallback

                try
                {
                    using (var context = new PrincipalContext(ContextType.Domain))
                    {
                        // Tenta di pulire lo username se arriva come DOMINIO\user
                        string cleanUser = adUsername.Contains("\\") ? adUsername.Split('\\')[1] : adUsername;

                        var userPrincipal = UserPrincipal.FindByIdentity(context, cleanUser);
                        if (userPrincipal != null)
                        {
                            // Usa il DisplayName perché è quello che salviamo nel DB come "Username"
                            userDisplayName = !string.IsNullOrEmpty(userPrincipal.DisplayName)
                                ? userPrincipal.DisplayName
                                : userPrincipal.Name;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore AD in GetTickets: {ex.Message}");
                }

                // Filtra: Ticket creati da me (Username) O creati per me (PerContoDi)
                // Case insensitive per sicurezza
                string searchName = userDisplayName.ToLower();
                query = query.Where(t =>
                    t.Username.ToLower() == searchName ||
                    (t.PerContoDi != null && t.PerContoDi.ToLower() == searchName)
                );
            }

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
                // Se non chiedo esplicitamente "includeAll", nascondo i terminati (StatoId = 3)
                // Questo vale anche per la vista "mine", così vedo solo Aperti/In Corso nella lista laterale
                if (!includeAll && !stato_id.HasValue && !nticket.HasValue)
                {
                    query = query.Where(t => t.StatoId != 3);
                }
            }

            if (nticket.HasValue) query = query.Where(t => t.Nticket == nticket.Value);
            if (assegnatoa_id.HasValue) query = query.Where(t => t.AssegnatoaId == assegnatoa_id.Value);
            if (tipologia_id.HasValue) query = query.Where(t => t.TipologiaId == tipologia_id.Value);
            if (urgenza_id.HasValue) query = query.Where(t => t.UrgenzaId == urgenza_id.Value);
            if (stato_id.HasValue) query = query.Where(t => t.StatoId == stato_id.Value);

            if (!string.IsNullOrEmpty(sede)) query = query.Where(t => t.Sede != null && t.Sede.Nome == sede);
            if (!string.IsNullOrEmpty(macchina)) query = query.Where(t => t.Macchina != null && t.Macchina.ToLower().Contains(macchina.ToLower()));

            // Filtro username classico (se non usiamo 'mine')
            if (!string.IsNullOrEmpty(username) && !mine) query = query.Where(t => t.Username.ToLower().Contains(username.ToLower()));

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
                    TipologiaColore = t.Tipologia != null ? t.Tipologia.Colore : null,
                    UrgenzaNome = t.Urgenza != null ? t.Urgenza.Nome : "N/D",
                    SedeNome = t.Sede != null ? t.Sede.Nome : "N/D",
                    StatoNome = t.Stato != null ? t.Stato.Nome : "N/D",
                    Username = t.Username,
                    Funzione = t.Funzione,
                    Macchina = t.Macchina,
                    AssegnatoaNome = t.Assegnatoa != null ? t.Assegnatoa.NomeCompleto : "Non assegnato",
                    DataCreazione = t.DataCreazione,
                    DataChiusura = t.DataChiusura,
                    UrgenzaCambiata = t.UrgenzaCambiata,
                    ScreenshotPath = t.ScreenshotPath,
                    StatoId = t.StatoId,
                    AssegnatoaId = t.AssegnatoaId,
                    TipologiaId = t.TipologiaId,
                    UrgenzaId = t.UrgenzaId,
                    Note = t.Note,
                    PerContoDi = t.PerContoDi,
                    SollecitiCount = t.Solleciti.Count()
                })
                .ToListAsync();

            return Ok(tickets);
        }

        // ... Resto dei metodi (UpdateTicket, CreateTicket, GetTipologie, etc.) rimangono uguali ...
        [HttpPut("{nticket}/update")]
        public async Task<IActionResult> UpdateTicket(int nticket, [FromBody] TicketUpdateRequest request)
        {
            var ticket = await _context.Ticket.FirstOrDefaultAsync(t => t.Nticket == nticket);
            if (ticket == null) return NotFound($"Ticket {nticket} non trovato.");

            bool modified = false;
            int ID_CRITICA = 4;

            if (request.StatoId.HasValue && ticket.StatoId != request.StatoId.Value)
            {
                ticket.StatoId = request.StatoId.Value;
                if (ticket.StatoId == 3)
                {
                    DateTime oraItaliana = DateTime.Now;
                    ticket.DataChiusura = DateTime.SpecifyKind(oraItaliana, DateTimeKind.Utc);
                    try
                    {
                        // Se c'è una nota nella request usiamo quella, altrimenti quella già nel ticket
                        string noteRisoluzione = request.Note ?? ticket.Note ?? "Nessuna nota";
                        await NotifyUserViaTeams(ticket, noteRisoluzione);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Errore notifica Teams: {ex.Message}");
                        // Non blocchiamo il return Ok()
                    }
                }
                else
                {
                    ticket.DataChiusura = null;
                }
                modified = true;
            }
            if (request.UrgenzaId.HasValue && ticket.UrgenzaId != request.UrgenzaId.Value)
            {
                // Se la NUOVA urgenza è Critica (e quella vecchia non lo era, o anche se lo era)
                if (request.UrgenzaId.Value == ID_CRITICA)
                {
                    // Lanciamo il broadcast in background (senza awaitare per non bloccare l'interfaccia utente)
                    _ = Task.Run(() => BroadcastCriticalAlert(ticket));
                }

                ticket.UrgenzaId = request.UrgenzaId.Value;
                ticket.UrgenzaCambiata = true;
                modified = true;
            }
            if (request.Note != null)
            {
                if (ticket.Note != request.Note) { ticket.Note = request.Note; modified = true; }
            }

            if (request.AssegnatoaId.HasValue || request.AssegnatoaId == null)
            {
                int? idDaSalvare = request.AssegnatoaId == 0 ? null : request.AssegnatoaId;
                if (ticket.AssegnatoaId != idDaSalvare) { ticket.AssegnatoaId = idDaSalvare; modified = true; }
            }

            if (request.UrgenzaId.HasValue && ticket.UrgenzaId != request.UrgenzaId.Value)
            {
                ticket.UrgenzaId = request.UrgenzaId.Value;
                ticket.UrgenzaCambiata = true;
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
        private async Task NotifyUserViaTeams(Ticket ticket, string notes)
        {
            string webhookUrl = _configuration["TeamsWebhookUrl"];
            if (string.IsNullOrEmpty(webhookUrl)) return;

            // 1. Determina chi notificare (Il creatore o il "Per Conto Di")
            string targetDisplayName = !string.IsNullOrEmpty(ticket.PerContoDi)
                ? ticket.PerContoDi
                : ticket.Username;

            // 2. Recupera l'EMAIL (UserPrincipalName) da AD usando il DisplayName
            string targetEmail = GetEmailFromDisplayName(targetDisplayName);

            if (string.IsNullOrEmpty(targetEmail))
            {
                System.Diagnostics.Debug.WriteLine($"Impossibile trovare email per {targetDisplayName}");
                return;
            }

            // 3. Prepara il payload JSON
            var payload = new
            {
                ticketNumber = ticket.Nticket,
                title = ticket.Titolo,
                userEmail = targetEmail, // Power Automate userà questa per la chat diretta
                notes = notes
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 4. Invia la richiesta HTTP POST
            using (var client = new HttpClient())
            {
                var response = await client.PostAsync(webhookUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore webhook Teams: {response.StatusCode}");
                }
            }
        }

        // Metodo per cercare l'email in AD partendo dal Nome (simile a quello che usavi in GetTickets)
        private string? GetEmailFromDisplayName(string displayName)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    // Cerchiamo per DisplayName (es. "Mario Rossi")
                    var userPrincipal = new UserPrincipal(context);
                    userPrincipal.DisplayName = displayName;

                    // Nota: FindOneByExample potrebbe non essere precisissimo se ci sono omonimi, 
                    // ma dato che salvi il DisplayName nel ticket, è il modo migliore per tornare indietro.
                    var searcher = new PrincipalSearcher(userPrincipal);
                    var result = searcher.FindOne() as UserPrincipal;

                    if (result != null)
                    {
                        // Restituisce l'indirizzo email o lo UserPrincipalName (che di solito è l'email in Azure AD/Teams)
                        return !string.IsNullOrEmpty(result.EmailAddress) ? result.EmailAddress : result.UserPrincipalName;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore AD Lookup: {ex.Message}");
            }
            return null;
        }
        [HttpPost("{nticket}/sollecita")]
        public async Task<IActionResult> SollecitaTicket(int nticket)
        {
            var ticket = await _context.Ticket.FirstOrDefaultAsync(t => t.Nticket == nticket);

            if (ticket == null) return NotFound($"Ticket numero {nticket} non trovato.");

            // 1. Salviamo SOLO il sollecito nel DB
            var nuovoSollecito = new Sollecito
            {
                TicketId = ticket.Id,
                DataSollecito = DateTime.UtcNow
            };

            _context.Solleciti.Add(nuovoSollecito);
            await _context.SaveChangesAsync();

            // NESSUNA NOTIFICA TEAMS QUI

            return Ok(new { message = "Sollecito registrato con successo" });
        }

        // --- AGGIUNGI QUESTO METODO HELPER ALLA FINE DEL CONTROLLER ---

        private async Task BroadcastCriticalAlert(Ticket ticket)
        {
            string webhookUrl = _configuration["TeamsCriticalUrl"];
            if (string.IsNullOrEmpty(webhookUrl)) return;

            try
            {
                // 1. Recupera tutti gli utenti IT dal database
                var itUsers = await _context.ItUtenti.ToListAsync();

                // 2. Prepara il payload base (uguale per tutti)
                var payloadObj = new
                {
                    ticketNumber = ticket.Nticket,
                    title = $"[CRITICO] {ticket.Titolo}", // Aggiungo un prefisso per evidenziarlo
                    notes = "ATTENZIONE: Ticket Critico Aperto/Aggiornato. Richiesto intervento immediato.",
                    userEmail = "" // Lo riempiremo nel ciclo
                };

                using (var client = new HttpClient())
                {
                    // 3. Ciclo su ogni utente IT
                    foreach (var user in itUsers)
                    {
                        // Recupera l'email da AD (usando il metodo che abbiamo fatto prima)
                        // Usa NomeCompleto se c'è, altrimenti UsernameAd
                        string nameToSearch = !string.IsNullOrEmpty(user.NomeCompleto) ? user.NomeCompleto : user.UsernameAd;
                        string email = GetEmailFromDisplayName(nameToSearch);

                        if (string.IsNullOrEmpty(email)) continue; // Se non troviamo l'email, saltiamo

                        // Aggiorna l'email nel payload
                        var currentPayload = new
                        {
                            payloadObj.ticketNumber,
                            payloadObj.title,
                            payloadObj.notes,
                            userEmail = email
                        };

                        var json = JsonSerializer.Serialize(currentPayload);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        // Invia la notifica (Non usiamo await per non bloccare troppo il ciclo, o usiamo await se vogliamo sicurezza)
                        try
                        {
                            await client.PostAsync(webhookUrl, content);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Errore invio a {email}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore Broadcast: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTicket([FromForm] TicketRequest request)
        {
            // 1. Recupero dati utente (come prima)
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
            catch { }

            // 2. Validazione riferimenti
            var urgenza = await _context.Urgenza.FirstOrDefaultAsync(u => u.Nome == request.Urgency);
            var tipologia = await _context.Tipologie.FirstOrDefaultAsync(t => t.Nome == request.ProblemType);
            var sede = await _context.Sedi.FirstOrDefaultAsync(s => s.Nome == request.Sede);

            if (urgenza == null || tipologia == null || sede == null) return BadRequest("Dati non validi.");

            // 3. Creiamo l'oggetto Ticket (senza screenshot per ora)
            var newTicket = new Ticket
            {
                Username = userDisplayName ?? "Sconosciuto",
                Funzione = request.Funzione,
                Titolo = request.Title,
                Testo = request.Message,
                ScreenshotPath = null, // Lo imposteremo dopo aver avuto l'ID
                DataCreazione = DateTime.UtcNow,
                Macchina = request.Macchina,
                TipologiaId = tipologia.Id,
                UrgenzaId = urgenza.Id,
                SedeId = sede.Id,
                PerContoDi = request.PerContoDi
            };

            // 4. Salviamo per generare l'ID / Nticket
            _context.Ticket.Add(newTicket);
            await _context.SaveChangesAsync();

            int ID_CRITICA = 4; // Controlla il tuo ID

            // Se nasce già critico
            if (newTicket.UrgenzaId == ID_CRITICA)
            {
                _ = Task.Run(() => BroadcastCriticalAlert(newTicket));
            }
            // 5. Gestione Upload Screenshot con nome personalizzato
            if (request.Screenshot != null && request.Screenshot.Length > 0)
            {
                try
                {
                    // Percorso base
                    var targetFolder = @"\\szblbfs01\zblb$\group_utenti\Inter_Uffici\Ticketmanager";
                    if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

                    // Recuperiamo i dati per il nome file
                    // Usa Nticket se disponibile, altrimenti Id come fallback
                    int ticketNumber = newTicket.Nticket > 0 ? newTicket.Nticket : newTicket.Id;

                    // Puliamo lo username da caratteri scomodi per i file (spazi, backslash, ecc)
                    string safeUsername = newTicket.Username.Replace(" ", "").Replace("\\", "").Replace("/", "").Trim();
                    string extension = Path.GetExtension(request.Screenshot.FileName);

                    // Calcolo Progressivo: Username+Nticket+p+X
                    int progressivo = 1;
                    string fileName = $"{safeUsername}{ticketNumber}p{progressivo}{extension}";
                    string filePath = Path.Combine(targetFolder, fileName);

                    // Verifica se esiste già un file con questo nome (es. p1) e incrementa se necessario
                    while (System.IO.File.Exists(filePath))
                    {
                        progressivo++;
                        fileName = $"{safeUsername}{ticketNumber}p{progressivo}{extension}";
                        filePath = Path.Combine(targetFolder, fileName);
                    }

                    // Salva il file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.Screenshot.CopyToAsync(stream);
                    }

                    // Aggiorna il record nel DB con il percorso definitivo
                    newTicket.ScreenshotPath = filePath;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Logga l'errore ma non bloccare la creazione del ticket se l'upload fallisce
                    System.Diagnostics.Debug.WriteLine($"Errore upload screenshot: {ex.Message}");
                }
            }

            return Ok(newTicket);
        }

        [HttpGet("tipologie")]
        public async Task<IActionResult> GetTipologie() { var data = await _context.Tipologie.Select(t => new { t.Id, t.Nome, t.Colore }).ToListAsync(); return Ok(data); }

        [HttpGet("urgenze")]
        public async Task<IActionResult> GetUrgenze() { var data = await _context.Urgenza.Select(u => new { u.Id, u.Nome }).ToListAsync(); return Ok(data); }

        [HttpGet("sedi")]
        public async Task<IActionResult> GetSedi() { var data = await _context.Sedi.Select(s => s.Nome).ToListAsync(); return Ok(data); }

        [HttpGet("stati")]
        public async Task<IActionResult> GetAllStati() { var stati = await _context.Stati.OrderBy(s => s.Id).Select(s => new { s.Id, s.Nome }).ToListAsync(); return Ok(stati); }
    }
}