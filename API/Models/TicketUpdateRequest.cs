namespace TicketAPI.Models
{
    /// <summary>
    /// Modello DTO (Data Transfer Object) utilizzato per ricevere
    /// richieste di aggiornamento parziali (PUT) dal ClientIT.
    /// I campi sono 'nullable' (int?) perché il client
    /// invierà *solo* il campo che è stato modificato.
    /// </summary>
    public class TicketUpdateRequest
    {
        public int? StatoId { get; set; }
        public int? AssegnatoaId { get; set; }
        public int? UrgenzaId { get; set; }   
        public int? TipologiaId { get; set; } 
    }
}