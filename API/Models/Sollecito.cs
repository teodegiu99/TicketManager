using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TicketAPI.Models
{
    [Table("sollecito")]
    public class Sollecito
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("ticketid")]
        public int TicketId { get; set; } // Punta a Ticket.Id

        [Column("datasollecito")]
        public DateTime DataSollecito { get; set; } = DateTime.UtcNow;

        [ForeignKey("TicketId")]
        public virtual Ticket? Ticket { get; set; }
    }
}