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
using Microsoft.Extensions.DependencyInjection;

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
        private readonly IServiceScopeFactory _serviceScopeFactory;
        public TicketsController(ApiDbContext context, IWebHostEnvironment env, IConfiguration configuration, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
            _serviceScopeFactory = scopeFactory;
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

            [FromForm(Name = "UtentiCC")]
            public string? UtentiCC { get; set; }

            [FromForm(Name = "Screenshots")]
            public List<IFormFile>? Screenshots { get; set; } 
        }

        public class TicketUpdateRequest
        {
            public int? StatoId { get; set; }
            public int? AssegnatoaId { get; set; }
            public int? UrgenzaId { get; set; }
            public int? TipologiaId { get; set; }
            public string? Note { get; set; }
            public bool? ChiusoDaUtente { get; set; } 
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
        (t.PerContoDi != null && t.PerContoDi.ToLower() == searchName) ||
        (t.UtentiCC != null && t.UtentiCC.ToLower().Contains(searchName)) // <--- AGGIUNTO
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
                    UtentiCC = t.UtentiCC, // <--- AGGIUNGI QUESTA RIGA QUI
                    AssegnatoaNome = t.Assegnatoa != null ? t.Assegnatoa.NomeCompleto : "Non assegnato",
                    DataCreazione = t.DataCreazione,
                    DataChiusura = t.DataChiusura,
                    UrgenzaCambiata = t.UrgenzaCambiata,
                    Allegati = t.Allegati.Select(a => new
                    {
                        Path = a.FilePath
                    }).ToList(),
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

                // --- NUOVA LOGICA: Salva se è stato chiuso dall'utente ---
                if (request.ChiusoDaUtente.HasValue && request.ChiusoDaUtente.Value == true)
                {
                    ticket.ChiusoDaUtente = true;
                }

                if (ticket.StatoId == 2)
                {
                    // Lanciamo la notifica in background per non bloccare l'UI
                    _ = Task.Run(() => NotifyUserInProgressViaTeams(ticket));
                }
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

                // Verifichiamo se l'assegnatario è cambiato
                if (ticket.AssegnatoaId != idDaSalvare)
                {
                    ticket.AssegnatoaId = idDaSalvare;
                    modified = true;

                    // SE c'è un nuovo assegnatario (idDaSalvare non è null), mandiamo la notifica
                    if (idDaSalvare.HasValue)
                    {
                        // Lanciamo il task in background passando ID Ticket e ID Assegnatario
                        // Usiamo i valori attuali per evitare problemi di concorrenza
                        int tId = ticket.Nticket;
                        int uId = idDaSalvare.Value;
                        _ = Task.Run(() => NotifyAssigneeViaTeams(tId, uId));
                    }
                }
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

            // 1. Creiamo la lista degli utenti da notificare (Creatore/Per Conto Di + CC)
            var displayNamesToNotify = new List<string>();
            string mainUser = !string.IsNullOrEmpty(ticket.PerContoDi) ? ticket.PerContoDi : ticket.Username;
            displayNamesToNotify.Add(mainUser);

            if (!string.IsNullOrWhiteSpace(ticket.UtentiCC))
            {
                var ccList = ticket.UtentiCC.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var cc in ccList)
                {
                    displayNamesToNotify.Add(cc.Trim());
                }
            }

            // Rimuoviamo duplicati (se un utente ha messo in CC se stesso)
            displayNamesToNotify = displayNamesToNotify.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            using (var client = new HttpClient())
            {
                foreach (var displayName in displayNamesToNotify)
                {
                    string targetEmail = GetEmailFromDisplayName(displayName);
                    if (string.IsNullOrEmpty(targetEmail)) continue;

                    var payload = new
                    {
                        ticketNumber = ticket.Nticket,
                        title = ticket.Titolo,
                        userEmail = targetEmail,
                        notes = notes
                    };

                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    try
                    {
                        var response = await client.PostAsync(webhookUrl, content);
                        if (!response.IsSuccessStatusCode)
                        {
                            System.Diagnostics.Debug.WriteLine($"Errore webhook Teams (Chiusura) per {targetEmail}: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Errore eccezione webhook Teams per {targetEmail}: {ex.Message}");
                    }
                }
            }
        }

        private async Task NotifyUserInProgressViaTeams(Ticket ticket)
        {
            string webhookUrl = _configuration["TeamsInCorsoUrl"];
            if (string.IsNullOrEmpty(webhookUrl)) return;

            var displayNamesToNotify = new List<string>();
            string mainUser = !string.IsNullOrEmpty(ticket.PerContoDi) ? ticket.PerContoDi : ticket.Username;
            displayNamesToNotify.Add(mainUser);

            if (!string.IsNullOrWhiteSpace(ticket.UtentiCC))
            {
                var ccList = ticket.UtentiCC.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var cc in ccList)
                {
                    displayNamesToNotify.Add(cc.Trim());
                }
            }

            displayNamesToNotify = displayNamesToNotify.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            using (var client = new HttpClient())
            {
                foreach (var displayName in displayNamesToNotify)
                {
                    string targetEmail = GetEmailFromDisplayName(displayName);
                    if (string.IsNullOrEmpty(targetEmail)) continue;

                    var payload = new
                    {
                        ticketNumber = ticket.Nticket,
                        title = ticket.Titolo,
                        userEmail = targetEmail,
                        notes = "Il tuo ticket è stato preso in carico dal reparto IT."
                    };

                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    try
                    {
                        await client.PostAsync(webhookUrl, content);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Errore eccezione webhook Teams InCorso per {targetEmail}: {ex.Message}");
                    }
                }
            }
        }

        private async Task NotifyAssigneeViaTeams(int ticketNumber, int assigneeId)
        {
            string webhookUrl = _configuration["TeamsAssignmentUrl"];
            if (string.IsNullOrEmpty(webhookUrl)) return;

            // Creiamo uno scope nuovo per il thread background
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var backgroundContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

                try
                {
                    // 1. Recuperiamo i dati dell'utente IT (l'assegnatario)
                    var itUser = await backgroundContext.ItUtenti.FindAsync(assigneeId);
                    if (itUser == null) return;

                    // 2. Recuperiamo il titolo del ticket (serve per il messaggio)
                    var ticketInfo = await backgroundContext.Ticket
                        .Where(t => t.Nticket == ticketNumber)
                        .Select(t => new { t.Titolo })
                        .FirstOrDefaultAsync();

                    if (ticketInfo == null) return;

                    // 3. Cerchiamo l'email in AD
                    string nameToSearch = !string.IsNullOrEmpty(itUser.NomeCompleto) ? itUser.NomeCompleto : itUser.UsernameAd;

                    // Nota: GetEmailFromDisplayName crea internamente il suo contesto AD, quindi è sicuro chiamarlo qui
                    string targetEmail = GetEmailFromDisplayName(nameToSearch);

                    if (string.IsNullOrEmpty(targetEmail))
                    {
                        System.Diagnostics.Debug.WriteLine($"Email non trovata per assegnatario: {nameToSearch}");
                        return;
                    }

                    // 4. Invio a Power Automate
                    var payload = new
                    {
                        ticketNumber = ticketNumber,
                        title = ticketInfo.Titolo,
                        assigneeEmail = targetEmail,
                        notes = "Ti è stato assegnato un nuovo ticket."
                    };

                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using (var client = new HttpClient())
                    {
                        var response = await client.PostAsync(webhookUrl, content);
                        if (!response.IsSuccessStatusCode)
                        {
                            System.Diagnostics.Debug.WriteLine($"Errore notifica assegnazione: {response.StatusCode}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore NotifyAssigneeViaTeams: {ex.Message}");
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

            // Creiamo uno scope esplicito per il background task
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                // Otteniamo un NUOVO contesto db che vive solo per la durata di questo metodo
                var backgroundContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

                try
                {
                    // 1. Recupera tutti gli utenti IT dal database usando il contesto BACKGROUND
                    var itUsers = await backgroundContext.ItUtenti.ToListAsync();

                    // 2. Prepara il payload base
                    var payloadObj = new
                    {
                        ticketNumber = ticket.Nticket,
                        title = $"[CRITICO] {ticket.Titolo}",
                        notes = "ATTENZIONE: Ticket Critico Aperto/Aggiornato. Richiesto intervento immediato.",
                        userEmail = ""
                    };

                    using (var client = new HttpClient())
                    {
                        foreach (var user in itUsers)
                        {
                            // Nota: GetEmailFromDisplayName crea il suo contesto AD, quindi è ok.
                            string nameToSearch = !string.IsNullOrEmpty(user.NomeCompleto) ? user.NomeCompleto : user.UsernameAd;
                            string email = GetEmailFromDisplayName(nameToSearch);

                            if (string.IsNullOrEmpty(email)) continue;

                            var currentPayload = new
                            {
                                payloadObj.ticketNumber,
                                payloadObj.title,
                                payloadObj.notes,
                                userEmail = email
                            };

                            var json = JsonSerializer.Serialize(currentPayload);
                            var content = new StringContent(json, Encoding.UTF8, "application/json");

                            try
                            {
                                var response = await client.PostAsync(webhookUrl, content);
                                // Log di debug più esplicito
                                if (!response.IsSuccessStatusCode)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Errore PowerAutomate per {email}: {response.StatusCode}");
                                }
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
                    System.Diagnostics.Debug.WriteLine($"Errore Broadcast nel task async: {ex.Message}");
                }
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
                DataCreazione = DateTime.UtcNow,
                Macchina = request.Macchina,
                TipologiaId = tipologia.Id,
                UrgenzaId = urgenza.Id,
                SedeId = sede.Id,
                PerContoDi = request.PerContoDi,
                UtentiCC = request.UtentiCC, // Salvataggio dei CC
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
            if (request.Screenshots != null && request.Screenshots.Count > 0)
            {
                var targetFolder = @"\\szblbfs01\zblb$\group_utenti\Inter_Uffici\Ticketmanager";
                string safeUsername = newTicket.Username.Replace(" ", "").Trim();
                int ticketNumber = newTicket.Nticket;

                foreach (var file in request.Screenshots)
                {
                    string extension = Path.GetExtension(file.FileName);
                    int progressivo = 1;
                    string fileName = $"{safeUsername}{ticketNumber}p{progressivo}{extension}";
                    string filePath = Path.Combine(targetFolder, fileName);

                    while (System.IO.File.Exists(filePath))
                    {
                        progressivo++;
                        fileName = $"{safeUsername}{ticketNumber}p{progressivo}{extension}";
                        filePath = Path.Combine(targetFolder, fileName);
                    }

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Salva il riferimento nella nuova tabella
                    _context.TicketAllegati.Add(new TicketAllegato
                    {
                        TicketId = newTicket.Id,
                        FilePath = filePath
                    });
                }
                await _context.SaveChangesAsync();
            }

            return Ok(newTicket);
        }

        [HttpGet("teamviewer/{macchina}")]
        public async Task<IActionResult> GetTeamViewerId(string macchina)
        {
            if (string.IsNullOrWhiteSpace(macchina)) return BadRequest("Nome macchina non fornito.");

            var tv = await _context.TeamViewerMachines
                .FirstOrDefaultAsync(t => t.NomeMacchina.ToLower() == macchina.ToLower());

            if (tv == null) return NotFound("Macchina non trovata su TeamViewer.");

            return Ok(new { idtw = tv.IdTw });
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