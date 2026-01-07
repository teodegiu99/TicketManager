using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ClientIT.Models
{
    public class ProjectViewModel
    {
        public int Id { get; set; }
        public string Titolo { get; set; }
        public string Descrizione { get; set; }
        public int StatoId { get; set; }
        public int? AssegnatoAId { get; set; }
        public string StatoNome { get; set; }
        public DateTime? DataInizio { get; set; }
        public DateTime? DataPrevFine { get; set; }

        public Stato Stato { get; set; }
        public ItUtente AssegnatoA { get; set; }

        public List<PhaseViewModel> Fasi { get; set; } = new();

        // --- FIX: Proprietà sicura per il binding nella lista ---
        // Se AssegnatoA è null, restituisce "Non assegnato" invece di far crashare l'app
        public string AssegnatoANome => AssegnatoA?.Nome ?? "Non assegnato";

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

        public string Initials => !string.IsNullOrEmpty(Username) ? Username.Substring(0, 1).ToUpper() : "?";
        public string DataFormat => DataCreazione.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        public Microsoft.UI.Xaml.HorizontalAlignment Allineamento { get; set; } = Microsoft.UI.Xaml.HorizontalAlignment.Left;
        public Microsoft.UI.Xaml.Media.SolidColorBrush Sfondo { get; set; }
    }
}