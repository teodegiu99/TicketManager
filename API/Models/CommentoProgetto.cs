using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TicketAPI.Models
{
    [Table("commenti_progetti")]
    public class CommentoProgetto
    {
        [Key][Column("id")] public int Id { get; set; }
        [Column("progetto_id")] public int ProgettoId { get; set; }
        [Column("utente_id")] public int? UtenteId { get; set; }
        [Column("username")] public string? Username { get; set; }
        [Column("testo")] public string Testo { get; set; } = string.Empty;
        [Column("datacreazione")] public DateTime DataCreazione { get; set; } = DateTime.UtcNow;
    }
}