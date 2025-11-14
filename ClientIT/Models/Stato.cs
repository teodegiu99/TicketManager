namespace ClientIT.Models
{
    // Questo modello rappresenta un oggetto "Stato"
    // ricevuto dall'API (es. { "id": 1, "nome": "Non assegnato" })
    public class Stato
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
    }
}