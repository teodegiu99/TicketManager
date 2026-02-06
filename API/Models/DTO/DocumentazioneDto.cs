using System.Collections.Generic;

namespace TicketAPI.Models.DTO
{
    public class DocumentazioneDto
    {
        public int Id { get; set; }
        public int Nticket { get; set; }
        public string Titolo { get; set; }
        public string Soluzione { get; set; }

        // Dati Tipologia (Ora opzionali per l'update)
        public int CategoriaId { get; set; }
        public string? CategoriaNome { get; set; }
        public string? CategoriaColore { get; set; }

        public string? Query { get; set; }

        // Dati Keywords (Ora opzionali per l'update)
        public int[]? KeywordIds { get; set; }
        public List<string>? KeywordNomi { get; set; }
    }

    // DTO per l'inserimento facilitato
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