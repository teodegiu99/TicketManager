using ClientIT.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized; // Necessario per NotifyCollectionChangedEventArgs
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ClientIT.Controls
{
    // =========================
    // WRAPPER PER LA VISUALIZZAZIONE ROADMAP
    // =========================
    public class RoadmapItem
    {
        public string Titolo { get; set; } = string.Empty;
        public Thickness Margin { get; set; } // Posizione barra
        public Thickness TextMargin { get; set; } // Posizione testo
        public double Width { get; set; }     // Lunghezza
        public SolidColorBrush Color { get; set; }
        public string TooltipText { get; set; } = string.Empty;
        public string DateText { get; set; } = string.Empty;
        public PhaseViewModel OriginalPhase { get; set; }
    }

    public class TimelineLabel
    {
        public string Text { get; set; }
        public Thickness Margin { get; set; }
    }

    public sealed partial class NewProjectControl : UserControl, INotifyPropertyChanged
    {
        // =========================
        // DATI
        // =========================
        public ObservableCollection<PhaseViewModel> Phases { get; } = new();
        public ObservableCollection<RoadmapItem> RoadmapItems { get; } = new();
        public ObservableCollection<TimelineLabel> TimelineLabels { get; } = new();
        public ObservableCollection<ItUtente> UsersOptions { get; } = new();
        public ObservableCollection<Stato> StatusOptions { get; } = new();

        private List<ItUtente> _allUsers = new();
        private List<Stato> _allStati = new();
        private readonly HttpClient _apiClient;

        // =========================
        // PROPRIETÀ ROADMAP
        // =========================
        private double _roadmapWidth = 800;
        private bool _hasPhases;

        public double RoadmapWidth { get => _roadmapWidth; set { _roadmapWidth = value; OnPropertyChanged(); } }
        public bool HasPhases { get => _hasPhases; set { _hasPhases = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public NewProjectControl()
        {
            InitializeComponent();
            DataContext = this;
            PhasesListView.ItemsSource = Phases;

            _apiClient = new HttpClient(new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });

            // ============================================================
            // 1. ASCOLTA I CAMBIAMENTI NELLA LISTA (Aggiunta/Rimozione Fasi)
            // ============================================================
            Phases.CollectionChanged += Phases_CollectionChanged;
        }

        // Gestione eventi: Quando aggiungo o tolgo fasi dalla lista
        private void Phases_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Se sono stati aggiunti nuovi elementi, ascolta le loro modifiche interne
            if (e.NewItems != null)
            {
                foreach (PhaseViewModel item in e.NewItems)
                {
                    item.PropertyChanged += Phase_PropertyChanged;
                }
            }

            // Se sono stati rimossi elementi, smetti di ascoltare
            if (e.OldItems != null)
            {
                foreach (PhaseViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= Phase_PropertyChanged;
                }
            }

            // Aggiorna sempre la roadmap quando la lista cambia
            GenerateRoadmap();
        }

        // Gestione eventi: Quando cambio una proprietà (Data, Titolo) di una singola fase
        private void Phase_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Aggiorna solo se cambiano dati rilevanti per il grafico
            if (e.PropertyName == nameof(PhaseViewModel.DataInizio) ||
                e.PropertyName == nameof(PhaseViewModel.DataPrevFine) ||
                e.PropertyName == nameof(PhaseViewModel.Titolo))
            {
                GenerateRoadmap();
            }
        }

        public void SetupReferenceData(List<ItUtente> users, List<Stato> stati)
        {
            _allUsers = users ?? new();
            _allStati = stati ?? new();

            UsersOptions.Clear();
            foreach (var u in _allUsers) UsersOptions.Add(u);

            StatusOptions.Clear();
            foreach (var s in _allStati) StatusOptions.Add(s);

        }

        // =========================
        // LOGICA GENERAZIONE ROADMAP (Automatica)
        // =========================
        private void GenerateRoadmap()
        {
            // Pulisci tutto prima di ridisegnare
            RoadmapItems.Clear();
            TimelineLabels.Clear();

            var validPhases = Phases
                .Where(p => p.DataInizio.HasValue && p.DataPrevFine.HasValue)
                .OrderBy(p => p.DataInizio)
                .ToList();

            if (!validPhases.Any())
            {
                HasPhases = false;
                return;
            }

            HasPhases = true;

            // 1. Calcolo limiti temporali
            var minDate = validPhases.Min(p => p.DataInizio!.Value.UtcDateTime);
            var maxDate = validPhases.Max(p => p.DataPrevFine!.Value.UtcDateTime);

            var viewStart = minDate.AddDays(-3);
            var viewEnd = maxDate.AddDays(5);
            var totalDays = (viewEnd - viewStart).TotalDays;
            if (totalDays < 1) totalDays = 1;

            // 2. Impostazioni grafiche
            double pixelsPerDay = 40;
            RoadmapWidth = totalDays * pixelsPerDay;

            // 3. Generazione Timeline
            for (int i = 0; i <= totalDays; i++)
            {
                var currentDate = viewStart.AddDays(i);
                TimelineLabels.Add(new TimelineLabel
                {
                    Text = currentDate.ToString("dd/MM"),
                    Margin = new Thickness(i * pixelsPerDay, 0, 0, 0)
                });
            }

            // 4. Generazione Barre
            foreach (var p in validPhases)
            {
                var startOffsetDays = (p.DataInizio!.Value.UtcDateTime - viewStart).TotalDays;
                var durationDays = (p.DataPrevFine!.Value.UtcDateTime - p.DataInizio!.Value.UtcDateTime).TotalDays;

                if (durationDays < 1) durationDays = 1;

                double leftPx = startOffsetDays * pixelsPerDay;
                double widthPx = durationDays * pixelsPerDay;
                double textLeftPx = leftPx + widthPx + 8;

                RoadmapItems.Add(new RoadmapItem
                {
                    Titolo = p.Titolo,
                    OriginalPhase = p,
                    Margin = new Thickness(leftPx, 0, 0, 0),
                    TextMargin = new Thickness(textLeftPx, 0, 0, 0),
                    Width = widthPx,
                    Color = new SolidColorBrush(Colors.Orange),
                    DateText = $"{p.DataInizio:dd/MM} - {p.DataPrevFine:dd/MM}",
                    TooltipText = $"{p.Titolo}\n{p.Descrizione}\n{p.DataInizio:dd/MM/yyyy} -> {p.DataPrevFine:dd/MM/yyyy}"
                });
            }
        }

        // =========================
        // GESTIONE LISTA E DETTAGLIO
        // =========================
        private void BtnAddPhase_Click(object sender, RoutedEventArgs e)
        {
            // Aggiungendo alla collezione, scatterà l'evento Phases_CollectionChanged -> GenerateRoadmap
            Phases.Add(new PhaseViewModel { Titolo = "Nuova fase" });
        }

        private void BtnRemovePhase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is PhaseViewModel p)
                Phases.Remove(p); // Anche qui scatta CollectionChanged -> GenerateRoadmap
        }

        private void PhasesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PhaseViewModel phase)
            {
                _ = ShowPhaseDetailAsync(phase);
            }
        }

        private void RoadmapItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RoadmapItem item)
            {
                _ = ShowPhaseDetailAsync(item.OriginalPhase);
            }
        }

        private async Task ShowPhaseDetailAsync(PhaseViewModel phase)
        {
            var dialogControl = new PhaseDetailDialog();
            dialogControl.Setup(_allUsers, _allStati, phase);

            var dialog = new ContentDialog
            {
                Title = "Dettaglio Fase",
                Content = dialogControl,
                PrimaryButtonText = "Salva",
                CloseButtonText = "Annulla",
                XamlRoot = XamlRoot
            };

            dialog.Closing += (s, args) =>
            {
                if (args.Result == ContentDialogResult.Primary && !dialogControl.Validate())
                    args.Cancel = true;
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var updated = dialogControl.GetPhase();

                // NOTA: Aggiornando queste proprietà, scatta l'evento PropertyChanged
                // che abbiamo agganciato nel costruttore/CollectionChanged.
                // Quindi GenerateRoadmap() verrà chiamato AUTOMATICAMENTE riga per riga.

                phase.Titolo = updated.Titolo;
                phase.Descrizione = updated.Descrizione;
                phase.DataInizio = updated.DataInizio;
                phase.DataPrevFine = updated.DataPrevFine;
                phase.Stato = updated.Stato;
                phase.AssegnatoA = updated.AssegnatoA;
            }
        }

        // =========================
        // SALVATAGGIO
        // =========================
        private async void BtnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTitolo.Text))
            {
                await ShowDialog("Errore", "Il titolo del progetto è obbligatorio.");
                return;
            }
            var selectedStato = CmbStato.SelectedItem as Stato;
            var selectedUser = CmbAssegnatoA.SelectedItem as ItUtente;
            BtnSaveProject.IsEnabled = false;
            SavingRing.Visibility = Visibility.Visible;
            SavingRing.IsActive = true;

            try
            {
                var dto = new
                {
                    Titolo = TxtTitolo.Text,
                    Descrizione = TxtDescrizione.Text,

                    // NUOVI CAMPI INVIATI AL SERVER
                    StatoId = selectedStato?.Id ?? 1, // Default 1 se null
                    AssegnatoAId = (selectedUser != null && selectedUser.Id > 0) ? (int?)selectedUser.Id : null,

                    Fasi = Phases.Select((p, i) => new
                    {
                        Titolo = p.Titolo,
                        Descrizione = p.Descrizione,
                        DataInizio = p.DataInizio?.UtcDateTime, // Importante: UTC
                        DataPrevFine = p.DataPrevFine?.UtcDateTime, // Importante: UTC
                        StatoId = p.Stato?.Id ?? 1,
                        Ordine = i,
                        AssegnatoAId = (p.AssegnatoA != null && p.AssegnatoA.Id > 0) ? (int?)p.AssegnatoA.Id : null
                    }).ToList()
                };

                var res = await _apiClient.PostAsJsonAsync("http://localhost:5210/api/progetti", dto);

                if (res.IsSuccessStatusCode)
                {
                    await ShowDialog("Successo", "Progetto creato correttamente!");
                    Phases.Clear(); // Pulisce lista -> Scatta CollectionChanged -> Pulisce Roadmap
                    ResetForm();
                }
                else
                {
                    await ShowDialog("Errore API", await res.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                await ShowDialog("Errore", ex.Message);
            }
            finally
            {
                BtnSaveProject.IsEnabled = true;
                SavingRing.IsActive = false;
                SavingRing.Visibility = Visibility.Collapsed;
            }
        }

        private async Task ShowDialog(string title, string content)
        {
            if (XamlRoot == null) return;
            await new ContentDialog { Title = title, Content = content, CloseButtonText = "OK", XamlRoot = XamlRoot }.ShowAsync();
        }

        private void ResetForm()
        {
            TxtTitolo.Text = "";
            TxtDescrizione.Text = "";

            // Resetta le ComboBox
            CmbStato.SelectedIndex = -1;
            CmbAssegnatoA.SelectedIndex = -1;

            Phases.Clear(); // Questo scatena anche la pulizia della Roadmap
        }
    }
}