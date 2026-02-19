using System.ComponentModel.DataAnnotations.Schema;

namespace TicketAPI.Models
{
    public class TeamViewerMachine
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nomemacchina")]
        public string NomeMacchina { get; set; }

        [Column("idtw")]
        public string IdTw { get; set; }
    }
}