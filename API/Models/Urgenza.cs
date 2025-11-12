// Models/Tipologia.cs
using System.ComponentModel.DataAnnotations.Schema; // <-- AGGIUNGI QUESTO

namespace TicketAPI.Models
{
    public class Urgenza
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nome")]
        public string Nome { get; set; }
    }
}