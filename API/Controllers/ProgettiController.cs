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

        // DTO per ricevere i dati dal client
        public class CreateProjectRequest
        {
            public string Titolo { get; set; }
            public string Descrizione { get; set; }
            public List<CreatePhaseDto> Fasi { get; set; }
        }

        public class CreatePhaseDto
        {
            public string Titolo { get; set; }
            public string Descrizione { get; set; }
            public DateTime? DataInizio { get; set; }
            public DateTime? DataPrevFine { get; set; }
            public int? AssegnatoAId { get; set; } // ID Utente o null
            public string? AssegnatoAEsterno { get; set; } // Per gestire l'utente esterno
            public int StatoId { get; set; }
            public int Ordine { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Crea il Progetto
                var nuovoProgetto = new Progetto
                {
                    Titolo = request.Titolo,
                    Descrizione = request.Descrizione,
                    DataInizio = DateTime.UtcNow, // Data creazione progetto
                    // Calcola la fine prevista come la max data fine delle fasi
                    DataPrevFine = request.Fasi.Any(f => f.DataPrevFine.HasValue)
                        ? request.Fasi.Max(f => f.DataPrevFine)
                        : null
                };

                _context.Progetti.Add(nuovoProgetto);
                await _context.SaveChangesAsync(); // Qui otteniamo l'ID del progetto

                // 2. Crea le Fasi collegate
                foreach (var faseDto in request.Fasi)
                {
                    var nuovaFase = new FaseProgetto
                    {
                        ProgettoId = nuovoProgetto.Id, // COLLEGAMENTO FONDAMENTALE
                        Titolo = faseDto.Titolo,
                        Descrizione = faseDto.Descrizione,
                        DataInizio = faseDto.DataInizio.HasValue ? DateTime.SpecifyKind(faseDto.DataInizio.Value, DateTimeKind.Utc) : null,
                        DataPrevFine = faseDto.DataPrevFine.HasValue ? DateTime.SpecifyKind(faseDto.DataPrevFine.Value, DateTimeKind.Utc) : null,
                        StatoId = faseDto.StatoId,
                        Ordine = faseDto.Ordine,
                    };

                    // Gestione AssegnatoA (campo stringa sul DB)
                    if (faseDto.AssegnatoAId.HasValue)
                    {
                        // Se è un ID, cerchiamo lo username (opzionale, o salviamo l'ID se hai cambiato la colonna, 
                        // ma per ora il DB ha 'assegnatoa' varchar, quindi salviamo lo string username o l'ID come stringa)
                        // Assumiamo di salvare l'ID come stringa per coerenza con la logica precedente
                        nuovaFase.AssegnatoA = faseDto.AssegnatoAId.ToString();
                    }
                    else if (!string.IsNullOrEmpty(faseDto.AssegnatoAEsterno))
                    {
                        nuovaFase.AssegnatoA = "Utente Esterno";
                    }

                    _context.FasiProgetto.Add(nuovaFase);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { Message = "Progetto creato con successo", ProjectId = nuovoProgetto.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Errore salvataggio: {ex.Message}");
            }
        }
    }
}