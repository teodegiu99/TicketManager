
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
        public List<int>? TipologieAbilitate { get; set; }
    }
}