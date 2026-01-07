using ClientIT.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI;

namespace ClientIT.Controls
{
    // Classi di supporto per la roadmap
 

    public sealed partial class ProjectDetailControl : UserControl, INotifyPropertyChanged
    {
        // =========================
        // DATI PRINCIPALI
        // =========================
        private ProjectViewModel _project;
        public ProjectViewModel Project
        {
            get => _project;
            set { _project = value; OnPropertyChanged(); }
        }

        private ItUtente _currentUser;
        private readonly HttpClient _apiClient;
        private bool _isLoadingData = false;

        // EVENTO BACK
        public event EventHandler BackRequested;

        // COLLEZIONI UI
        public ObservableCollection<PhaseViewModel> Phases { get; } = new();
        public ObservableCollection<RoadmapItem> RoadmapItems { get; } = new();
        public ObservableCollection<TimelineLabel> TimelineLabels { get; } = new();
        public ObservableCollection<CommentoViewModel> Comments { get; } = new();

        // CACHE & OPZIONI
        public ObservableCollection<Stato> StatusOptions { get; } = new();
        public ObservableCollection<ItUtente> UsersOptions { get; } = new();
        private List<Stato> _allStatiCache = new();
        private List<ItUtente> _allUsersCache = new();

        // PROPRIETÀ GRAFICHE
        private double _roadmapWidth = 800;
        private bool _hasPhases;
        public double RoadmapWidth { get => _roadmapWidth; set { _roadmapWidth = value; OnPropertyChanged(); } }
        public bool HasPhases { get => _hasPhases; set { _hasPhases = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ProjectDetailControl()
        {
            this.InitializeComponent();
            _apiClient = new HttpClient(new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });

            // Reattività Roadmap
            Phases.CollectionChanged += Phases_CollectionChanged;
        }

        // =========================
        // CARICAMENTO (LOAD)
        // =========================
        public void Load(ProjectViewModel projectSummary, ItUtente currentUser, List<Stato> stati, List<ItUtente> utenti)
        {
            _isLoadingData = true;
            _currentUser = currentUser;

            // 1. Setup Cache
            _allStatiCache = stati ?? new();
            _allUsersCache = utenti ?? new();

            StatusOptions.Clear();
            foreach (var s in _allStatiCache) StatusOptions.Add(s);

            UsersOptions.Clear();
            foreach (var u in _allUsersCache) UsersOptions.Add(u);

            // 2. Setup Progetto Base
            Project = projectSummary;

            // 3. Caricamento Dettagli Asincrono
            _ = LoadFullDetails(projectSummary.Id);
            _ = LoadComments(); // <--- TUA PARTE COMMENTI
        }

        private async Task LoadFullDetails(int projectId)
        {
            try
            {
                // Scarica il progetto completo
                var fullProject = await _apiClient.GetFromJsonAsync<ProjectViewModel>($"http://localhost:5210/api/progetti/{projectId}");

                if (fullProject != null)
                {
                    // Aggiorna l'oggetto Project principale
                    Project.Descrizione = fullProject.Descrizione;
                    Project.Titolo = fullProject.Titolo;
                    Project.Stato = _allStatiCache.FirstOrDefault(s => s.Id == fullProject.Stato?.Id);
                    Project.AssegnatoA = _allUsersCache.FirstOrDefault(u => u.Id == fullProject.AssegnatoA?.Id);

                    // Aggiorna UI ComboBox manualmente per sicurezza
                    CmbStato.SelectedValue = Project.Stato?.Id;
                    CmbAssegnatoA.SelectedValue = Project.AssegnatoA?.Id;

                    // Popola Fasi (Scatta roadmap automatica)
                    Phases.Clear();
                    if (fullProject.Fasi != null)
                    {
                        foreach (var f in fullProject.Fasi.OrderBy(x => x.Ordine))
                        {
                            Phases.Add(f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore LoadDetails: {ex.Message}");
            }
            finally
            {
                _isLoadingData = false;
            }
        }
        private async Task OpenPhaseDialog(PhaseViewModel phase)
        {
            if (phase == null) return;

            var dialogControl = new PhaseDetailDialog();
            dialogControl.Setup(_allUsersCache, _allStatiCache, phase);

            var dialog = new ContentDialog
            {
                Title = "Modifica Fase",
                Content = dialogControl,
                PrimaryButtonText = "Applica",
                CloseButtonText = "Annulla",
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var updated = dialogControl.GetPhase();
                // Aggiorna i dati...
                phase.Titolo = updated.Titolo;
                phase.Descrizione = updated.Descrizione;
                phase.DataInizio = updated.DataInizio;
                phase.DataPrevFine = updated.DataPrevFine;
                phase.Stato = updated.Stato;
                phase.AssegnatoA = updated.AssegnatoA;
            }
        }
        // =========================
        // LOGICA ROADMAP (Engine)
        // =========================
        private void Phases_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (PhaseViewModel item in e.NewItems) item.PropertyChanged += Phase_PropertyChanged;

            if (e.OldItems != null)
                foreach (PhaseViewModel item in e.OldItems) item.PropertyChanged -= Phase_PropertyChanged;

            GenerateRoadmap();
        }

        private void Phase_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PhaseViewModel.DataInizio) ||
                e.PropertyName == nameof(PhaseViewModel.DataPrevFine) ||
                e.PropertyName == nameof(PhaseViewModel.Titolo))
            {
                GenerateRoadmap();
            }
        }

        private void GenerateRoadmap()
        {
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

            var minDate = validPhases.Min(p => p.DataInizio!.Value.UtcDateTime);
            var maxDate = validPhases.Max(p => p.DataPrevFine!.Value.UtcDateTime);

            var viewStart = minDate.AddDays(-3);
            var viewEnd = maxDate.AddDays(5);
            var totalDays = Math.Max(1, (viewEnd - viewStart).TotalDays);

            double pixelsPerDay = 45;
            RoadmapWidth = totalDays * pixelsPerDay;

            // Timeline
            for (int i = 0; i <= totalDays; i++)
            {
                TimelineLabels.Add(new TimelineLabel
                {
                    Text = viewStart.AddDays(i).ToString("dd/MM"),
                    Margin = new Thickness(i * pixelsPerDay, 0, 0, 0)
                });
            }

            // Barre
            foreach (var p in validPhases)
            {
                var startOffset = (p.DataInizio!.Value.UtcDateTime - viewStart).TotalDays;
                var duration = (p.DataPrevFine!.Value.UtcDateTime - p.DataInizio!.Value.UtcDateTime).TotalDays;
                if (duration < 1) duration = 1;

                RoadmapItems.Add(new RoadmapItem
                {
                    Titolo = p.Titolo,
                    OriginalPhase = p,
                    Margin = new Thickness(startOffset * pixelsPerDay, 0, 0, 0),
                    Width = duration * pixelsPerDay,
                    Color = new SolidColorBrush(Colors.CornflowerBlue),
                    DateText = $"{p.DataInizio:dd/MM} - {p.DataPrevFine:dd/MM}",
                    TextMargin = new Thickness((startOffset * pixelsPerDay) + (duration * pixelsPerDay) + 8, 0, 0, 0),
                    TooltipText = $"{p.Titolo}\n{p.Descrizione}"
                });
            }
        }

        // =========================
        // GESTIONE COMMENTI (TUA LOGICA)
        // =========================
        private async Task LoadComments()
        {
            try
            {
                Comments.Clear();
                // Scarica i commenti dal server
                var list = await _apiClient.GetFromJsonAsync<List<CommentoViewModel>>($"http://localhost:5210/api/progetti/{Project.Id}/commenti");

                if (list != null)
                {
                    foreach (var c in list)
                    {
                        // Determina se il commento è dell'utente corrente
                        // (Controlla sia UsernameAD che Nome per sicurezza)
                        bool isMe = (c.Username == _currentUser?.Nome) ||
                                    (c.Username == _currentUser?.UsernameAd);

                        // IMPOSTAZIONE UI BASATA SUL TUO MODELLO

                        // 1. Allineamento (Destra per me, Sinistra per gli altri)
                        c.Allineamento = isMe
                            ? HorizontalAlignment.Right
                            : HorizontalAlignment.Left;

                        // 2. Colore Sfondo (Azzurrino per me, Grigio chiaro per gli altri)
                        c.Sfondo = isMe
                            ? new SolidColorBrush(Color.FromArgb(255, 220, 240, 255)) // Azzurro tenue
                            : new SolidColorBrush(Colors.WhiteSmoke);                 // Grigio

                        // Aggiungi alla collezione osservabile
                        Comments.Add(c);
                    }

                    // Scroll automatico all'ultimo messaggio
                    if (Comments.Any())
                    {
                        await Task.Delay(50); // Piccolo delay per permettere il rendering UI
                        CommentsListView.ScrollIntoView(Comments.Last());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore caricamento commenti: {ex.Message}");
            }
        }

        private async void BtnSendComment_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCommento.Text) || Project == null) return;

            var dto = new
            {
                Testo = TxtCommento.Text,
                UtenteId = _currentUser?.Id ?? 1,
                Username = _currentUser?.Nome ?? "Utente"
            };

            try
            {
                var res = await _apiClient.PostAsJsonAsync($"http://localhost:5210/api/progetti/{Project.Id}/commenti", dto);
                if (res.IsSuccessStatusCode)
                {
                    TxtCommento.Text = "";
                    await LoadComments(); // Ricarica lista
                }
            }
            catch { }
        }

        // =========================
        // GESTIONE AZIONI FASI
        // =========================
        private void BtnAddPhase_Click(object sender, RoutedEventArgs e)
        {
            var startDate = DateTimeOffset.Now;
            if (Phases.Any() && Phases.Last().DataPrevFine.HasValue)
                startDate = Phases.Last().DataPrevFine!.Value.AddDays(1);

            Phases.Add(new PhaseViewModel
            {
                Id = 0,
                Titolo = "Nuova Fase",
                DataInizio = startDate,
                DataPrevFine = startDate.AddDays(5),
                Stato = _allStatiCache.FirstOrDefault()
            });
        }

        private void BtnRemovePhase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is PhaseViewModel p) Phases.Remove(p);
        }

        private async void PhasesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PhaseViewModel phase)
            {
                await OpenPhaseDialog(phase);
            }
        }

        // Gestione Click dalla Roadmap
        private async void RoadmapItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RoadmapItem item)
            {
                // Chiamiamo direttamente il metodo comune, SENZA inventare ItemClickEventArgs
                await OpenPhaseDialog(item.OriginalPhase);
            }
        }

        // =========================
        // SALVATAGGIO & BACK
        // =========================
        private async void BtnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            // Logica salvataggio completa (invia tutto il pacchetto fasi + dettagli)
            var dto = new
            {
                Id = Project.Id,
                Titolo = TxtTitolo.Text,
                Descrizione = TxtDescrizione.Text,
                StatoId = (int?)CmbStato.SelectedValue,
                AssegnatoAId = (int?)CmbAssegnatoA.SelectedValue,
                Fasi = Phases.Select((p, i) => new
                {
                    Id = p.Id,
                    Titolo = p.Titolo,
                    Descrizione = p.Descrizione,
                    DataInizio = p.DataInizio?.DateTime,
                    DataPrevFine = p.DataPrevFine?.DateTime,
                    StatoId = p.Stato?.Id ?? 1,
                    Ordine = i,
                    AssegnatoAId = (p.AssegnatoA?.Id > 0) ? (int?)p.AssegnatoA.Id : null
                }).ToList()
            };

            try
            {
                var res = await _apiClient.PutAsJsonAsync($"http://localhost:5210/api/progetti/{Project.Id}", dto);
                if (res.IsSuccessStatusCode)
                {
                    await new ContentDialog { Title = "Salvato", Content = "Progetto aggiornato!", CloseButtonText = "Ok", XamlRoot = XamlRoot }.ShowAsync();
                    await LoadFullDetails(Project.Id);
                }
            }
            catch (Exception ex)
            {
                // Error handling
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
        private void TxtCommento_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                BtnSendComment_Click(sender, new RoutedEventArgs());
            }
        }
    }
}