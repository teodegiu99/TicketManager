using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ClientIT.Models
{
    // Questo modello viene inviato dall'API (AuthController/users)
    // L'API invia JSON in PascalCase (es. "UsernameAd")
    // Il deserializzatore usa PropertyNameCaseInsensitive = true
    public class ItUtente
    {
        public int Id { get; set; }
        public string UsernameAd { get; set; }
        public string Permesso { get; set; }

        public string Nome { get; set; } = string.Empty;
        public List<int>? TipologieAbilitate { get; set; }

        // Aggiungi questa proprietà statica per "Non assegnato"
        public static ItUtente NonAssegnato { get; } = new ItUtente
        {
            Id = 0,
            UsernameAd = "Non assegnato",
            Permesso = "Nessuno",
            Nome = "Non assegnato"
        };
    }
}