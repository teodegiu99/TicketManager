using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic; // Aggiunto per List

namespace TicketAPI.Models
{
    [Table("ticket")]
    public class Ticket
    {
        // --- MODIFICA 1: Riportato a INT ---
        [Key]
        [Column("nticket")]
        // --- MODIFICA 2: Aggiunto [DatabaseGenerated] ---
        // Questo dice a EF Core di lasciare che il DB generi il valore
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Nticket { get; set; } // Era string

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("funzione")]
        public string? Funzione { get; set; }

        [Column("titolo")]
        public string Titolo { get; set; } = string.Empty;

        [Column("testo")]
        public string Testo { get; set; } = string.Empty;

        [Column("screenshotpath")]
        public string? ScreenshotPath { get; set; }

        [Column("datacreazione")]
        public DateTime DataCreazione { get; set; }

        [Column("macchina")]
        public string? Macchina { get; set; }

        // --- Chiavi Esterne ---
        [Column("tipologiaid")]
        public int TipologiaId { get; set; }

        [Column("urgenzaid")]
        public int UrgenzaId { get; set; }

        [Column("sedeid")]
        public int SedeId { get; set; }

        [Column("assegnatoaid")]
        public int? AssegnatoaId { get; set; }

        [Column("statoid")]
        public int StatoId { get; set; } = 1;

        // --- Proprietà di Navigazione (queste sono corrette) ---
        [ForeignKey("TipologiaId")]
        public virtual Tipologia? Tipologia { get; set; }

        [ForeignKey("UrgenzaId")]
        public virtual Urgenza? Urgenza { get; set; }

        [ForeignKey("SedeId")]
        public virtual Sede? Sede { get; set; }

        [ForeignKey("StatoId")]
        public virtual Stato? Stato { get; set; }

        [ForeignKey("AssegnatoaId")]
        public virtual ItUtente? Assegnatoa { get; set; }
    }
}