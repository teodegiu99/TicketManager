using System.ComponentModel.DataAnnotations.Schema;

namespace TicketAPI.Models
{
    [Table("stato")]
    public class Stato
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nome")]
        public string Nome { get; set; }
    }
}