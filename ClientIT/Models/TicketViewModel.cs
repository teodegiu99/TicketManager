using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ClientIT.Models
{
    public class TicketViewModel : INotifyPropertyChanged
    {
        // --- Implementazione INotifyPropertyChanged ---
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Campi privati
        private int _statoId;
        private int? _assegnatoaId;
        private string _statoNome = string.Empty;
        private string _assegnatoaNome = string.Empty;

        // Proprietà standard
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

        // --- PROPRIETÀ COLLEGATE AI COMBOBOX ---

        public int StatoId
        {
            get => _statoId;
            set
            {
                if (_statoId != value)
                {
                    _statoId = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? AssegnatoaId
        {
            // TRUCCO: Se è null, restituisci 0 (così seleziona "Non Assegnato" nel ComboBox)
            get => _assegnatoaId ?? 0;
            set
            {
                if (_assegnatoaId != value)
                {
                    _assegnatoaId = value;
                    OnPropertyChanged();
                }
            }
        }

        // Aggiorniamo anche i testi se necessario
        public string StatoNome
        {
            get => _statoNome;
            set { _statoNome = value; OnPropertyChanged(); }
        }

        public string AssegnatoaNome
        {
            get => _assegnatoaNome;
            set { _assegnatoaNome = value; OnPropertyChanged(); }
        }
    }
}