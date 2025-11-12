// Models/Ticket.cs
using System.ComponentModel.DataAnnotations.Schema; 

namespace TicketAPI.Models
{
    public class Ticket
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nticket")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] 
        public int Nticket { get; set; }

        [Column("username")]
        public string? Username { get; set; }

        [Column("funzione")]
        public string? Funzione { get; set; }

        [Column("titolo")]
        public string? Titolo { get; set; }

        [Column("testo")]
        public string? Testo { get; set; }

        [Column("screenshotpath")]
        public string? ScreenshotPath { get; set; }

        [Column("datacreazione")]
        public DateTime DataCreazione { get; set; }

        [Column("macchina")] 
        public string? Macchina { get; set; } 

        // Chiavi esterne
        [Column("tipologiaid")]
        public int TipologiaId { get; set; }

        [Column("urgenzaid")]
        public int UrgenzaId { get; set; }

        [Column("sedeid")]
        public int SedeId { get; set; }
    }
}