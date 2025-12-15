using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TicketAPI.Models
{
    [Table("fasiprogetto")]
    public class FaseProgetto
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("titolo")]
        public string? Titolo { get; set; }

        [Column("descrizione")]
        public string? Descrizione { get; set; }

        [Column("datainizio")]
        public DateTime? DataInizio { get; set; }

        [Column("dataprevfine")]
        public DateTime? DataPrevFine { get; set; }

        [Column("datachiusura")]
        public DateTime? DataChiusura { get; set; }

        // Manteniamo stringa per poter salvare "Utente Esterno" o lo username
        [Column("assegnatoa")]
        public string? AssegnatoA { get; set; }

        // Usiamo INT per uniformarci ai Ticket
        [Column("stato_id")]
        public int StatoId { get; set; } = 1;

        [Column("ordine")]
        public int Ordine { get; set; } = 0;

        // Navigazione solo per Stato (AssegnatoA è stringa libera/username)
        [ForeignKey("StatoId")]
        public virtual Stato? Stato { get; set; }

        [Column("progetto_id")]
        public int? ProgettoId { get; set; }
    }
}