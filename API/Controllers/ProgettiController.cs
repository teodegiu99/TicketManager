
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Data;
using TicketAPI.Models;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProgettiController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public ProgettiController(ApiDbContext context)
        {
            _context = context;
        }

        // GET: api/progetti/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAllProgetti()
        {
            var rawProjects = await _context.Progetti
                .OrderBy(p => p.StatoId)
                .ThenByDescending(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Titolo,
                    p.Descrizione,
                    p.StatoId,
                    DataInizio = p.DataInizio,
                    DataPrevFine = p.DataPrevFine,
                    StatoNome = _context.Stati.Where(s => s.Id == p.StatoId).Select(s => s.Nome).FirstOrDefault() ?? "-",
                    p.AssegnatoA // Stringa nel DB
                })
                .ToListAsync();

            var userIds = rawProjects
                .Where(p => int.TryParse(p.AssegnatoA, out _))
                .Select(p => int.Parse(p.AssegnatoA))
                .Distinct()
                .ToList();

            var users = await _context.ItUtenti
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var result = rawProjects.Select(p =>
            {
                ItUtente? user = null;
                if (int.TryParse(p.AssegnatoA, out int uid) && users.ContainsKey(uid))
                {
                    user = users[uid];
                }

                return new
                {
                    p.Id,
                    p.Titolo,
                    p.Descrizione,
                    p.StatoId,
                    p.DataInizio,
                    p.DataPrevFine,
                    p.StatoNome,
                    AssegnatoA = (user != null) ? new { user.Id, user.Nome } : null,
                    AssegnatoAId = user?.Id
                };
            });

            return Ok(result);
        }

        // GET: api/progetti/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProgetto(int id)
        {
            var progetto = await _context.Progetti.FindAsync(id);
            if (progetto == null) return NotFound("Progetto non trovato");

            var fasi = await _context.FasiProgetto
                .Where(f => f.ProgettoId == id)
                .OrderBy(f => f.Ordine)
                .ToListAsync();

            int assegnatoId = 0;
            if (int.TryParse(progetto.AssegnatoA, out int parsedId)) assegnatoId = parsedId;

            var result = new
            {
                progetto.Id,
                progetto.Titolo,
                progetto.Descrizione,
                progetto.StatoId,
                progetto.DataInizio,
                progetto.DataPrevFine,
                progetto.DataChiusura,
                AssegnatoA = new { Id = assegnatoId, Nome = "Caricamento..." },
                AssegnatoAId = assegnatoId > 0 ? (int?)assegnatoId : null,
                Fasi = fasi.Select(f => new
                {
                    f.Id,
                    f.Titolo,
                    f.Descrizione,
                    f.DataInizio,
                    f.DataPrevFine,
                    f.StatoId,
                    f.Ordine,
                    Stato = new { Id = f.StatoId, Nome = _context.Stati.Where(s => s.Id == f.StatoId).Select(s => s.Nome).FirstOrDefault() ?? "-" },
                    AssegnatoA = new { Id = 0, Nome = f.AssegnatoA ?? "Non assegnato" }
                }).ToList()
            };

            return Ok(result);
        }

        // POST: api/progetti
        [HttpPost]
        public async Task<IActionResult> CreateProgetto([FromBody] Progetto progetto)
        {
            if (progetto == null) return BadRequest();
            progetto.DataInizio = DateTime.SpecifyKind(progetto.DataInizio ?? DateTime.Now, DateTimeKind.Utc);
            if (progetto.DataPrevFine.HasValue) progetto.DataPrevFine = DateTime.SpecifyKind(progetto.DataPrevFine.Value, DateTimeKind.Utc);
            _context.Progetti.Add(progetto);
            await _context.SaveChangesAsync();
            return Ok(progetto);
        }

        // PUT: api/progetti/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectRequest request)
        {
            if (id != request.Id) return BadRequest("ID progetto non corrispondente.");
            var project = await _context.Progetti.FindAsync(id);
            if (project == null) return NotFound("Progetto non trovato.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                project.Titolo = request.Titolo;
                project.Descrizione = request.Descrizione;
                project.StatoId = request.StatoId;
                project.AssegnatoA = request.AssegnatoAId.HasValue ? request.AssegnatoAId.ToString() : null;

                if (request.Fasi != null)
                {
                    var existingPhases = await _context.FasiProgetto.Where(f => f.ProgettoId == id).ToListAsync();
                    var incomingIds = request.Fasi.Where(f => f.Id > 0).Select(f => f.Id).ToList();
                    var phasesToDelete = existingPhases.Where(f => !incomingIds.Contains(f.Id)).ToList();
                    if (phasesToDelete.Any()) _context.FasiProgetto.RemoveRange(phasesToDelete);

                    foreach (var faseDto in request.Fasi)
                    {
                        var startUtc = faseDto.DataInizio.HasValue ? DateTime.SpecifyKind(faseDto.DataInizio.Value, DateTimeKind.Utc) : (DateTime?)null;
                        var endUtc = faseDto.DataPrevFine.HasValue ? DateTime.SpecifyKind(faseDto.DataPrevFine.Value, DateTimeKind.Utc) : (DateTime?)null;

                        if (faseDto.Id > 0)
                        {
                            var existingPhase = existingPhases.FirstOrDefault(f => f.Id == faseDto.Id);
                            if (existingPhase != null)
                            {
                                existingPhase.Titolo = faseDto.Titolo;
                                existingPhase.Descrizione = faseDto.Descrizione;
                                existingPhase.DataInizio = startUtc;
                                existingPhase.DataPrevFine = endUtc;
                                existingPhase.StatoId = faseDto.StatoId;
                                existingPhase.Ordine = faseDto.Ordine;
                                existingPhase.AssegnatoA = faseDto.AssegnatoAId.HasValue ? faseDto.AssegnatoAId.ToString() : null;
                            }
                        }
                        else
                        {
                            _context.FasiProgetto.Add(new FaseProgetto
                            {
                                ProgettoId = id,
                                Titolo = faseDto.Titolo,
                                Descrizione = faseDto.Descrizione,
                                DataInizio = startUtc,
                                DataPrevFine = endUtc,
                                StatoId = faseDto.StatoId,
                                Ordine = faseDto.Ordine,
                                AssegnatoA = faseDto.AssegnatoAId.HasValue ? faseDto.AssegnatoAId.ToString() : null
                            });
                        }
                    }
                    if (request.Fasi.Any(f => f.DataPrevFine.HasValue))
                    {
                        var maxDate = request.Fasi.Where(f => f.DataPrevFine.HasValue).Max(f => f.DataPrevFine.Value);
                        project.DataPrevFine = DateTime.SpecifyKind(maxDate, DateTimeKind.Utc);
                    }
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { Message = "Progetto aggiornato con successo" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Errore: {ex.Message}");
            }
        }

        // --- METODI COMMENTI MANCANTI ---
        [HttpGet("{id}/commenti")]
        public async Task<IActionResult> GetCommenti(int id)
        {
            var commenti = await _context.CommentiProgetti
                .Where(c => c.ProgettoId == id)
                .OrderBy(c => c.DataCreazione)
                .Select(c => new
                {
                    c.Id,
                    c.Testo,
                    c.DataCreazione,
                    c.Username,
                    c.UtenteId
                })
                .ToListAsync();
            return Ok(commenti);
        }

        [HttpPost("{id}/commenti")]
        public async Task<IActionResult> PostCommento(int id, [FromBody] CommentoDto commento)
        {
            if (commento == null || string.IsNullOrWhiteSpace(commento.Testo)) return BadRequest();
            var nuovoCommento = new CommentoProgetto
            {
                ProgettoId = id,
                Testo = commento.Testo,
                UtenteId = commento.UtenteId,
                Username = commento.Username,
                DataCreazione = DateTime.UtcNow
            };
            _context.CommentiProgetti.Add(nuovoCommento);
            await _context.SaveChangesAsync();
            return Ok(nuovoCommento);
        }
    }

    public class UpdateProjectRequest
    {
        public int Id { get; set; }
        public string Titolo { get; set; }
        public string Descrizione { get; set; }
        public int StatoId { get; set; }
        public int? AssegnatoAId { get; set; }
        public List<UpdatePhaseDto> Fasi { get; set; }
    }

    public class UpdatePhaseDto
    {
        public int Id { get; set; }
        public string Titolo { get; set; }
        public string Descrizione { get; set; }
        public DateTime? DataInizio { get; set; }
        public DateTime? DataPrevFine { get; set; }
        public int StatoId { get; set; }
        public int Ordine { get; set; }
        public int? AssegnatoAId { get; set; }
    }

    public class CommentoDto
    {
        public string Testo { get; set; }
        public int UtenteId { get; set; }
        public string Username { get; set; }
    }
}