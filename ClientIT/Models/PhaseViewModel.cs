using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClientIT.Models
{
    public class PhaseViewModel : INotifyPropertyChanged
    {
        private string _titolo = string.Empty;
        private string _descrizione = string.Empty;
        private DateTimeOffset? _dataInizio;
        private DateTimeOffset? _dataPrevFine;
        private ItUtente? _assegnatoA;
        private Stato? _stato;

        public string TempId { get; } = Guid.NewGuid().ToString(); // ID temporaneo per la UI

        public string Titolo { get => _titolo; set { _titolo = value; OnPropertyChanged(); } }
        public string Descrizione { get => _descrizione; set { _descrizione = value; OnPropertyChanged(); } }

        public DateTimeOffset? DataInizio { get => _dataInizio; set { _dataInizio = value; OnPropertyChanged(); } }
        public DateTimeOffset? DataPrevFine { get => _dataPrevFine; set { _dataPrevFine = value; OnPropertyChanged(); } }

        public ItUtente? AssegnatoA { get => _assegnatoA; set { _assegnatoA = value; OnPropertyChanged(); } }
        public Stato? Stato { get => _stato; set { _stato = value; OnPropertyChanged(); } }

        public int Ordine { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}