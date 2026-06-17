using System.Collections.Generic;

namespace ClientIT.Models
{
    public class DocumentazioneDto
    {
        public int Id { get; set; }
        public int Nticket { get; set; }
        public string Titolo { get; set; }
        public string Soluzione { get; set; }
        public string? Query { get; set; }

        // Dati Tipologia (Nullable per evitare errori di validazione in invio)
        public int CategoriaId { get; set; }
        public string? CategoriaNome { get; set; }
        public string? CategoriaColore { get; set; }

        // Dati Keywords
        public int[]? KeywordIds { get; set; }
        public List<string>? KeywordNomi { get; set; }
    }

    public class CreateDocRequest
    {
        public int Nticket { get; set; }
        public string Titolo { get; set; }
        public string Soluzione { get; set; }
        public string? Query { get; set; }
        public int CategoriaId { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
    }
}
