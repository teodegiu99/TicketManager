using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TicketAPI.Models // O il tuo namespace (es: API.Models)
{
    [Table("documentazione")] // Forziamo il nome minuscolo per standard Postgres
    public class Documentazione
    {
        [Key]
        public int Id { get; set; }

        public int Nticket { get; set; }

        public string Titolo { get; set; } = string.Empty;

        public string Soluzione { get; set; } = string.Empty;

        public int Categoria { get; set; }
        public string? Query { get; set; } 

        // Mappatura specifica per array di interi in PostgreSQL
        [Column(TypeName = "integer[]")]
        public int[] Keywords { get; set; } = Array.Empty<int>();
    }
}