using System;

namespace ClientIT.Models
{
    public class ProjectViewModel
    {
        public int Id { get; set; }
        public string Titolo { get; set; }
        public string Descrizione { get; set; }
        public int StatoId { get; set; }
        public string StatoNome { get; set; }
        public DateTime? DataInizio { get; set; }
        public DateTime? DataPrevFine { get; set; }

        // Per il binding del colore stato nella lista
        public string StatoColor => StatoId switch
        {
            1 => "#3498db", // Nuovo (Blu)
            2 => "#f39c12", // In Corso (Arancio)
            3 => "#27ae60", // Terminato (Verde)
            _ => "#7f8c8d"
        };
    }

    public class CommentoViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Testo { get; set; }
        public DateTime DataCreazione { get; set; }

        // Formattazione per la UI
        public string Initials => !string.IsNullOrEmpty(Username) ? Username.Substring(0, 1).ToUpper() : "?";
        public string DataFormat => DataCreazione.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        // Allineamento (se sono io è a destra, altri a sinistra)
        // Lo gestiremo nel code-behind controllando l'utente loggato
        public Microsoft.UI.Xaml.HorizontalAlignment Allineamento { get; set; } = Microsoft.UI.Xaml.HorizontalAlignment.Left;
        public Microsoft.UI.Xaml.Media.SolidColorBrush Sfondo { get; set; }
    }
}