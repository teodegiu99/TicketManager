using System.ComponentModel.DataAnnotations.Schema;

namespace TicketAPI.Models
{
    public class ItUtente
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("username_ad")]
        public string UsernameAd { get; set; }

        [Column("permesso")]
        public string Permesso { get; set; }

        [Column("tipologie_abilitate")]
        public List<int>? TipologieAbilitate { get; set; }

        [Column("nome")]
        public string? Nome { get; set; }


        [Column("nomecompleto")] // Assicurati che il nome tra virgolette sia uguale a quello sul DB
        public string? NomeCompleto { get; set; }
    }
}