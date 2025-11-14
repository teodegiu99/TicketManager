using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ClientIT.Models
{
    // Questo modello viene inviato dall'API (AuthController)
    // L'API invia JSON in PascalCase (es. "UsernameAd")
    // Il JsonSerializer (in App.xaml.cs) usa PropertyNameCaseInsensitive = true,
    // quindi non abbiamo bisogno di [JsonPropertyName].
    public class ItAuthData
    {
        public int Id { get; set; }
        public string UsernameAd { get; set; }
        public string Permesso { get; set; }
        public List<int>? TipologieAbilitate { get; set; }
    }
}