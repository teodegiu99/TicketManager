using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TicketAPI.Models
{
    [Table("keywords")]
    public class Keyword
    {
        [Key]
        public int Id { get; set; }

        public string Nome { get; set; } = string.Empty;
    }
}