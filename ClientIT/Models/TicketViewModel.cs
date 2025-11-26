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

        // Campi privati per la gestione del binding (ComboBox)
        private int _statoId;
        private int? _assegnatoaId;

        private int _urgenzaId;
        private int _tipologiaId;

        private string _statoNome = string.Empty;
        private string _assegnatoaNome = string.Empty;
        private string _note = string.Empty;
        // Proprietà standard (Dati visuali o di sola lettura)
        public int Id { get; set; }
        public int Nticket { get; set; }
        public string Titolo { get; set; } = string.Empty;
        public string Testo { get; set; } = string.Empty;

        // Queste rimangono auto-props per ora, a meno che non serva aggiornare il testo manualmente
        public string TipologiaNome { get; set; } = string.Empty;
        public string UrgenzaNome { get; set; } = string.Empty;
        public string SedeNome { get; set; } = string.Empty;
        public DateTime? DataChiusura { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Funzione { get; set; } = string.Empty;
        public string Macchina { get; set; } = string.Empty;
        public DateTime DataCreazione { get; set; }
        public string ScreenshotPath { get; set; } = string.Empty;

        // --- PROPRIETÀ COLLEGATE AI COMBOBOX (MODIFICABILI) ---

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

        // NUOVA PROPRIETÀ: URGENZA
        public int UrgenzaId
        {
            get => _urgenzaId;
            set
            {
                if (_urgenzaId != value)
                {
                    _urgenzaId = value;
                    OnPropertyChanged();
                }
            }
        }

        // NUOVA PROPRIETÀ: TIPOLOGIA
        public int TipologiaId
        {
            get => _tipologiaId;
            set
            {
                if (_tipologiaId != value)
                {
                    _tipologiaId = value;
                    OnPropertyChanged();
                }
            }
        }

        // Aggiorniamo anche i testi se necessario (Utile se cambi ID e vuoi cambiare label, ma qui lo fa la combo)
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
        public string Note
        {
            get => _note;
            set { if (_note != value) { _note = value; OnPropertyChanged(); } }
        }

    }
}