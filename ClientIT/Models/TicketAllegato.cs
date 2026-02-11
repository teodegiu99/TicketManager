// In API/Models e ClientIT/Models
using System;

namespace ClientIT.Models
{
    public class TicketAllegato
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public string Path { get; set; }
        public DateTime DataCaricamento { get; set; }
    }
}