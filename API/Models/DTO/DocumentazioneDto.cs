namespace TicketAPI.Models.DTO
{
    public class DocumentazioneDto
    {
        public int Id { get; set; }
        public int Nticket { get; set; }
        public string Titolo { get; set; }
        public string Soluzione { get; set; }

        // Dati Tipologia
        public int CategoriaId { get; set; }
        public string CategoriaNome { get; set; }
        public string CategoriaColore { get; set; } // Utile per il frontend!
        public string? Query { get; set; }
        // Dati Keywords
        public int[] KeywordIds { get; set; }
        public List<string> KeywordNomi { get; set; }
    }
    // DTO per l'inserimento facilitato
    public class CreateDocRequest
    {
        public int Nticket { get; set; }
        public string Titolo { get; set; }
        public string Soluzione { get; set; }
        public string? Query { get; set; }
        public int CategoriaId { get; set; } // ID Tipologia
        public List<string> Keywords { get; set; } = new List<string>();
    }
}