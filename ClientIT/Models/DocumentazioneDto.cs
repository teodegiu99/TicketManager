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

        // Dati Tipologia
        public int CategoriaId { get; set; }
        public string CategoriaNome { get; set; }
        public string CategoriaColore { get; set; }

        // Dati Keywords
        public int[] KeywordIds { get; set; }
        public List<string> KeywordNomi { get; set; }
    }
}