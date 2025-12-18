using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TicketAPI.Models
{
    [Table("progetti")]
    public class Progetto
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("titolo")]
        public string? Titolo { get; set; }

        [Column("descrizione")]
        public string? Descrizione { get; set; }

        // Nota: Nel DB è varchar, quindi qui è string. 
        // Se in futuro diventerà una foreign key numerica, andrà cambiato in int.
        [Column("fasi_id")]
        public string? FasiId { get; set; }

        [Column("datainizio")]
        public DateTime? DataInizio { get; set; }

        [Column("dataprevfine")]
        public DateTime? DataPrevFine { get; set; }

        [Column("datachiusura")]
        public DateTime? DataChiusura { get; set; }

        [Column("assegnatoa")]
        public string? AssegnatoA { get; set; }

        [Column("statoid")]
        public int StatoId { get; set; }
    }
}