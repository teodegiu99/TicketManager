using System;

namespace ClientIT.Models
{
    // Questo modello rappresenta i dati inviati da TicketsController/all

    // MODIFICA: Inizializziamo tutte le stringhe a 'string.Empty'
    // e rimuoviamo il '?' (nullable).
    // Questo impedisce a x:Bind di crashare se l'API invia 'null'.

    public class TicketViewModel
    {
        public int Id { get; set; }
        public int Nticket { get; set; }
        public string Titolo { get; set; } = string.Empty;
        public string Testo { get; set; } = string.Empty;
        public string TipologiaNome { get; set; } = string.Empty;
        public string UrgenzaNome { get; set; } = string.Empty;
        public string SedeNome { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Funzione { get; set; } = string.Empty;
        public string Macchina { get; set; } = string.Empty;
        public DateTime DataCreazione { get; set; }
        public string ScreenshotPath { get; set; } = string.Empty;
        public string StatoNome { get; set; } = string.Empty;
        public string AssegnatoaNome { get; set; } = string.Empty;
        public int StatoId { get; set; }
        public int? AssegnatoaId { get; set; } // Nullable (può non essere assegnato)
    }
}