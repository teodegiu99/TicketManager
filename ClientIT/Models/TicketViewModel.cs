using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Windows.UI;

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
        private string? _tipologiaColore;
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
        public string? TipologiaColore
        {
            get => _tipologiaColore;
            set
            {
                if (_tipologiaColore != value)
                {
                    _tipologiaColore = value;
                    OnPropertyChanged();
                    // Notifica che anche il Brush è cambiato
                    OnPropertyChanged(nameof(TipologiaBrush));
                }
            }
        }

        // Proprietà per il Binding nello XAML (Converte Hex -> Brush)
        public SolidColorBrush TipologiaBrush
        {
            get
            {
                if (string.IsNullOrEmpty(TipologiaColore))
                {
                    // Colore di default (es. Blu sistema o Grigio)
                    return new SolidColorBrush(Color.FromArgb(255, 0, 120, 215));
                }
                try
                {
                    return new SolidColorBrush(GetColorFromHex(TipologiaColore));
                }
                catch
                {
                    return new SolidColorBrush(Colors.Gray);
                }
            }
        }

        // Helper per convertire stringa Hex in Color
        private Color GetColorFromHex(string hex)
        {
            hex = hex.Replace("#", "");
            byte a = 255;
            byte r = 255, g = 255, b = 255;
            int start = 0;

            if (hex.Length == 8) // C'è il canale Alpha
            {
                a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                start = 2;
            }

            r = byte.Parse(hex.Substring(start, 2), System.Globalization.NumberStyles.HexNumber);
            g = byte.Parse(hex.Substring(start + 2, 2), System.Globalization.NumberStyles.HexNumber);
            b = byte.Parse(hex.Substring(start + 4, 2), System.Globalization.NumberStyles.HexNumber);

            return Color.FromArgb(a, r, g, b);
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