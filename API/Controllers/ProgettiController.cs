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
                    DataPrevFine = dataFineProgetto // Ora è UTC sicuro
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
    }
}