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
    public class ProgettiController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public ProgettiController(ApiDbContext context)
        {
            _context = context;
        }

        public class CreateProjectRequest
        {
            public string Titolo { get; set; }
            public string Descrizione { get; set; }
            public int StatoId { get; set; }
            public int? AssegnatoAId { get; set; }
            public List<CreatePhaseDto> Fasi { get; set; }
        }

        public class CreatePhaseDto
        {
            public string Titolo { get; set; }
            public string Descrizione { get; set; }
            public DateTime? DataInizio { get; set; }
            public DateTime? DataPrevFine { get; set; }
            public int? AssegnatoAId { get; set; }
            public string? AssegnatoAEsterno { get; set; }
            public int StatoId { get; set; }
            public int Ordine { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // CORREZIONE: Calcoliamo la data massima assicurandoci che sia UTC
                DateTime? dataFineProgetto = null;

                if (request.Fasi != null && request.Fasi.Any(f => f.DataPrevFine.HasValue))
                {
                    // 1. Trova la data massima grezza
                    var maxDate = request.Fasi
                        .Where(f => f.DataPrevFine.HasValue)
                        .Max(f => f.DataPrevFine.Value);

                    // 2. Specifica che è UTC (Postgres lo richiede)
                    dataFineProgetto = DateTime.SpecifyKind(maxDate, DateTimeKind.Utc);
                }

                // 1. Crea il Progetto
                var nuovoProgetto = new Progetto
                {
                    Titolo = request.Titolo,
                    Descrizione = request.Descrizione,
                    DataInizio = DateTime.UtcNow,
                    DataPrevFine = dataFineProgetto, 
                    StatoId = request.StatoId,
                    AssegnatoA = request.AssegnatoAId.HasValue ? request.AssegnatoAId.ToString() : null
                };

                _context.Progetti.Add(nuovoProgetto);
                await _context.SaveChangesAsync();

                // 2. Crea le Fasi collegate
                if (request.Fasi != null)
                {
                    foreach (var faseDto in request.Fasi)
                    {
                        var nuovaFase = new FaseProgetto
                        {
                            ProgettoId = nuovoProgetto.Id,
                            Titolo = faseDto.Titolo,
                            Descrizione = faseDto.Descrizione,
                            // Anche qui forziamo UTC per sicurezza
                            DataInizio = faseDto.DataInizio.HasValue
                                ? DateTime.SpecifyKind(faseDto.DataInizio.Value, DateTimeKind.Utc)
                                : null,
                            DataPrevFine = faseDto.DataPrevFine.HasValue
                                ? DateTime.SpecifyKind(faseDto.DataPrevFine.Value, DateTimeKind.Utc)
                                : null,
                            StatoId = faseDto.StatoId,
                            Ordine = faseDto.Ordine,
                            DataChiusura = (faseDto.StatoId == 3) ? DateTime.UtcNow : null
                        };

                        if (faseDto.AssegnatoAId.HasValue)
                        {
                            nuovaFase.AssegnatoA = faseDto.AssegnatoAId.ToString();
                        }
                        else if (!string.IsNullOrEmpty(faseDto.AssegnatoAEsterno))
                        {
                            nuovaFase.AssegnatoA = "Utente Esterno";
                        }

                        _context.FasiProgetto.Add(nuovaFase);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { Message = "Progetto creato con successo", ProjectId = nuovoProgetto.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // Log dell'errore interno per debug
                Console.WriteLine($"Errore CreateProject: {ex}");
                return StatusCode(500, $"Errore salvataggio: {ex.Message}");
            }
        }

        // Classe di supporto per ricevere il nuovo stato dal client
        public class UpdateStatusRequest
        {
            public int StatoId { get; set; }
        }

        // =============================================
        // 1. AGGIORNA STATO FASE
        // =============================================
        [HttpPut("fasi/{id}/stato")]
        public async Task<IActionResult> UpdateFaseStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            var fase = await _context.FasiProgetto.FindAsync(id);
            if (fase == null) return NotFound("Fase non trovata");

            // Aggiorna lo stato
            fase.StatoId = request.StatoId;

            // LOGICA CHIUSURA:
            // Se lo stato diventa 3 (Terminato), imposta la data di chiusura a OGGI (UTC).
            // Se lo stato cambia in altro (es. torna in lavorazione), rimuovi la data di chiusura.
            if (request.StatoId == 3)
            {
                // Verifica se era già chiusa per non sovrascrivere la data originale (opzionale)
                if (!fase.DataChiusura.HasValue)
                {
                    fase.DataChiusura = DateTime.UtcNow;
                }
            }
            else
            {
                // Se riapro la fase, resetto la data
                fase.DataChiusura = null;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Stato fase aggiornato", DataChiusura = fase.DataChiusura });
        }

        // =============================================
        // 2. AGGIORNA STATO PROGETTO
        // =============================================
        [HttpPut("{id}/stato")]
        public async Task<IActionResult> UpdateProgettoStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            var progetto = await _context.Progetti.FindAsync(id);
            if (progetto == null) return NotFound("Progetto non trovato");

            progetto.StatoId = request.StatoId;

            // LOGICA CHIUSURA PROGETTO
            if (request.StatoId == 3) // 3 = Terminato
            {
                if (!progetto.DataChiusura.HasValue)
                {
                    progetto.DataChiusura = DateTime.UtcNow;
                }
            }
            else
            {
                progetto.DataChiusura = null;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Stato progetto aggiornato", DataChiusura = progetto.DataChiusura });
        }
        [HttpGet("all")]
        public async Task<IActionResult> GetAllProgetti()
        {
            // Recupera progetti con stato e conteggio fasi
            // Ordinamento richiesto: Aperti (1) -> In Corso (2) -> Terminati (3)
            // Assumiamo ID: 1=Nuovo, 2=InCorso, 3=Terminato. Se diversi, usa un CASE WHEN in SQL o ordina in memoria.
            var progetti = await _context.Progetti
                .OrderBy(p => p.StatoId) // 1, 2, 3...
                .ThenByDescending(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Titolo,
                    p.Descrizione,
                    p.StatoId,
                    DataInizio = p.DataInizio,
                    DataPrevFine = p.DataPrevFine,
                    StatoNome = _context.Stati.Where(s => s.Id == p.StatoId).Select(s => s.Nome).FirstOrDefault() ?? "Sconosciuto",
                    Avanzamento = 0 // Qui potresti calcolare % fasi completate
                })
                .ToListAsync();

            return Ok(progetti);
        }

        // =============================================
        // 4. GET COMMENTI
        // =============================================
        [HttpGet("{id}/commenti")]
        public async Task<IActionResult> GetCommenti(int id)
        {
            var commenti = await _context.CommentiProgetti
                .Where(c => c.ProgettoId == id)
                .OrderBy(c => c.DataCreazione)
                .ToListAsync();
            return Ok(commenti);
        }

        // =============================================
        // 5. ADD COMMENTO
        // =============================================
        public class AddCommentoRequest
        {
            public string Testo { get; set; }
            public int? UtenteId { get; set; }
            public string Username { get; set; }
        }

        [HttpPost("{id}/commenti")]
        public async Task<IActionResult> AddCommento(int id, [FromBody] AddCommentoRequest request)
        {
            var commento = new CommentoProgetto
            {
                ProgettoId = id,
                Testo = request.Testo,
                UtenteId = request.UtenteId,
                Username = request.Username,
                DataCreazione = DateTime.UtcNow
            };

            _context.CommentiProgetti.Add(commento);
            await _context.SaveChangesAsync();

            return Ok(commento);
        }
    }

}