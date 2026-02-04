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
using TicketManager;
using Windows.UI;

namespace ClientIT.Controls
{
    public sealed partial class ProjectDetailControl : UserControl, INotifyPropertyChanged
    {
        private ProjectViewModel _project;
        public ProjectViewModel Project
        {
            get => _project;
            set { _project = value; OnPropertyChanged(); }
        }

        private ItUtente _currentUser;
        private readonly HttpClient _apiClient;
        public event EventHandler BackRequested;

        // COLLEZIONI
        public ObservableCollection<PhaseViewModel> Phases { get; } = new();
        public ObservableCollection<RoadmapItem> RoadmapItems { get; } = new();
        public ObservableCollection<TimelineLabel> TimelineLabels { get; } = new();
        public ObservableCollection<CommentoViewModel> Comments { get; } = new();

        public ObservableCollection<Stato> StatusOptions { get; } = new();
        public ObservableCollection<ItUtente> UsersOptions { get; } = new();

        private List<Stato> _allStatiCache = new();
        private List<ItUtente> _allUsersCache = new();

        // Tracker per evitare memory leak
        private List<PhaseViewModel> _phasesEventTracker = new();

        private double _roadmapWidth = 800;
        private bool _hasPhasesBool;

        // --- FIX CRASH: Flag per gestire navigazione rapida ---
        private bool _suppressRoadmap = false;
        private int _currentLoadingId = -1;

        public Visibility HasPhases => _hasPhasesBool ? Visibility.Visible : Visibility.Collapsed;
        public double RoadmapWidth { get => _roadmapWidth; set { _roadmapWidth = value; OnPropertyChanged(); } }

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
            Phases.CollectionChanged += Phases_CollectionChanged;
        }

        public void Load(ProjectViewModel projectSummary, ItUtente currentUser, List<Stato> stati, List<ItUtente> utenti)
        {
            // Impostiamo l'ID corrente: invalida i caricamenti precedenti
            _currentLoadingId = projectSummary.Id;

            _currentUser = currentUser;
            _allStatiCache = stati ?? new();
            _allUsersCache = utenti ?? new();

            StatusOptions.Clear();
            foreach (var s in _allStatiCache) StatusOptions.Add(s);
            foreach (var u in _allUsersCache) UsersOptions.Add(u);

            Project = projectSummary;

            // Pulizia UI
            _suppressRoadmap = true;
            Phases.Clear();
            RoadmapItems.Clear();
            TimelineLabels.Clear();
            Comments.Clear();
            _suppressRoadmap = false;

            // Avvio caricamenti
            _ = LoadFullDetails(projectSummary.Id);
            _ = LoadComments(projectSummary.Id);
        }

        private async Task LoadFullDetails(int projectId)
        {
            try
            {
                var fullProject = await _apiClient.GetFromJsonAsync<ProjectViewModel>($"{ApiConfig.BaseUrl}/api/progetti/{projectId}");

                // SE L'UTENTE HA CAMBIATO PROGETTO, FERMATI
                if (_currentLoadingId != projectId) return;

                if (fullProject != null)
                {
                    fullProject.Stato = _allStatiCache.FirstOrDefault(s => s.Id == fullProject.StatoId);

                    var assegnatoId = fullProject.AssegnatoA?.Id ?? fullProject.AssegnatoAId;
                    fullProject.AssegnatoA = _allUsersCache.FirstOrDefault(u => u.Id == assegnatoId);
                    fullProject.AssegnatoAId = assegnatoId;

                    Project = fullProject;

                    _suppressRoadmap = true;
                    Phases.Clear();

                    if (fullProject.Fasi != null)
                    {
                        foreach (var f in fullProject.Fasi.OrderBy(x => x.Ordine))
                        {
                            if (f.Stato != null) f.Stato = _allStatiCache.FirstOrDefault(s => s.Id == f.Stato.Id);
                            if (f.AssegnatoA != null) f.AssegnatoA = _allUsersCache.FirstOrDefault(u => u.Id == f.AssegnatoA.Id);
                            Phases.Add(f);
                        }
                    }
                    _suppressRoadmap = false;
                    GenerateRoadmap();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore LoadDetails: {ex.Message}");
            }
        }

        private async Task LoadComments(int projectId)
        {
            try
            {
                Comments.Clear();
                var list = await _apiClient.GetFromJsonAsync<List<CommentoViewModel>>($"{ApiConfig.BaseUrl}/api/progetti/{projectId}/commenti");

                // FIX: Fermati se progetto cambiato
                if (_currentLoadingId != projectId) return;
                if (list != null)
                {
                    foreach (var c in list)
                    {
                        // 1. Determina se sono IO o un altro
                        bool isMe = (c.UtenteId == _currentUser?.Id);
                        // Nota: uso UtenteId che è più sicuro del nome, ma va bene anche la tua logica precedente

                        // 2. Impostazioni Grafiche
                        if (isMe)
                        {
                            // MESSAGGIO MIO: Allineato a destra, Blu Scuro, Testo Bianco
                            c.Allineamento = HorizontalAlignment.Right;
                            c.Sfondo = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215)); // Accent Blue
                            c.ColoreTesto = new SolidColorBrush(Microsoft.UI.Colors.White); // Contrasto alto
                        }
                        else
                        {
                            // MESSAGGIO ALTRUI: Allineato a sinistra, Grigio Chiaro, Testo Nero
                            c.Allineamento = HorizontalAlignment.Left;
                            c.Sfondo = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 230, 230)); // Grigio chiaro
                            c.ColoreTesto = new SolidColorBrush(Microsoft.UI.Colors.Black); // Testo scuro
                        }

                        // 3. Calcolo Iniziali (Nome + Cognome)
                        c.Iniziali = GetInitials(c.Username);

                        Comments.Add(c);
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Errore commenti: {ex.Message}"); }
        }
        private string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "?";

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                // Solo un nome: prendi le prime 2 lettere o solo la prima
                string name = parts[0];
                return name.Length > 1 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
            }

            if (parts.Length >= 2)
            {
                // Nome e Cognome: prendi la prima lettera del primo e la prima dell'ultimo
                char first = parts[0][0];
                char last = parts[parts.Length - 1][0];
                return $"{first}{last}".ToUpper();
            }

            return "?";
        }
        private void GenerateRoadmap()
        {
            if (_suppressRoadmap) return;
            // Controllo extra per evitare crash se Phases è null
            if (Phases == null) return;

            RoadmapItems.Clear();
            TimelineLabels.Clear();

            var validPhases = Phases.Where(p => p != null && p.DataInizio.HasValue && p.DataPrevFine.HasValue).OrderBy(p => p.DataInizio).ToList();

            if (!validPhases.Any())
            {
                if (_hasPhasesBool) { _hasPhasesBool = false; OnPropertyChanged(nameof(HasPhases)); }
                return;
            }

            if (!_hasPhasesBool) { _hasPhasesBool = true; OnPropertyChanged(nameof(HasPhases)); }

            var minDate = validPhases.Min(p => p.DataInizio!.Value.UtcDateTime);
            var maxDate = validPhases.Max(p => p.DataPrevFine!.Value.UtcDateTime);

            var viewStart = minDate.AddDays(-3);
            var viewEnd = maxDate.AddDays(5);
            var totalDays = Math.Max(1, (viewEnd - viewStart).TotalDays);

            double pixelsPerDay = 45;
            RoadmapWidth = totalDays * pixelsPerDay;

            for (int i = 0; i <= totalDays; i++)
            {
                TimelineLabels.Add(new TimelineLabel
                {
                    Text = viewStart.AddDays(i).ToString("dd/MM"),
                    Margin = new Thickness(i * pixelsPerDay, 0, 0, 0)
                });
            }

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

        private async void BtnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (Project == null) return;
            var dto = new
            {
                Id = Project.Id,
                Titolo = TxtTitolo.Text,
                Descrizione = TxtDescrizione.Text,
                StatoId = Project.StatoId,
                AssegnatoAId = (Project.AssegnatoAId > 0) ? Project.AssegnatoAId : null,
                Fasi = Phases.Select((p, i) => new
                {
                    Id = p.Id,
                    Titolo = p.Titolo,
                    Descrizione = p.Descrizione,
                    DataInizio = p.DataInizio?.UtcDateTime,
                    DataPrevFine = p.DataPrevFine?.UtcDateTime,
                    StatoId = p.Stato?.Id ?? 1,
                    Ordine = i,
                    AssegnatoAId = (p.AssegnatoA != null && p.AssegnatoA.Id > 0) ? (int?)p.AssegnatoA.Id : null
                }).ToList()
            };

            try
            {
                var res = await _apiClient.PutAsJsonAsync($"{ApiConfig.BaseUrl}/api/progetti/{Project.Id}", dto);
                if (res.IsSuccessStatusCode)
                {
                    await new ContentDialog { Title = "Salvato", Content = "Progetto aggiornato!", CloseButtonText = "Ok", XamlRoot = XamlRoot }.ShowAsync();
                    if (_currentLoadingId == Project.Id) await LoadFullDetails(Project.Id);
                }
                else
                {
                    string err = await res.Content.ReadAsStringAsync();
                    await new ContentDialog { Title = "Errore Salvataggio", Content = err, CloseButtonText = "Chiudi", XamlRoot = XamlRoot }.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                if (XamlRoot != null) await new ContentDialog { Title = "Eccezione", Content = ex.Message, CloseButtonText = "Chiudi", XamlRoot = XamlRoot }.ShowAsync();
            }
        }

        private void Phases_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var item in _phasesEventTracker) if (item != null) item.PropertyChanged -= Phase_PropertyChanged;
                _phasesEventTracker.Clear();
            }
            else
            {
                if (e.OldItems != null) foreach (PhaseViewModel item in e.OldItems) if (item != null) { item.PropertyChanged -= Phase_PropertyChanged; _phasesEventTracker.Remove(item); }
                if (e.NewItems != null) foreach (PhaseViewModel item in e.NewItems) if (item != null) { item.PropertyChanged += Phase_PropertyChanged; _phasesEventTracker.Add(item); }
            }
            if (!_suppressRoadmap) GenerateRoadmap();
        }

        private void Phase_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressRoadmap) return;
            if (e.PropertyName == nameof(PhaseViewModel.DataInizio) || e.PropertyName == nameof(PhaseViewModel.DataPrevFine) || e.PropertyName == nameof(PhaseViewModel.Titolo))
            {
                GenerateRoadmap();
            }
        }

        private async Task OpenPhaseDialog(PhaseViewModel phase)
        {
            if (phase == null || XamlRoot == null) return;
            var dialogControl = new PhaseDetailDialog();
            dialogControl.Setup(_allUsersCache, _allStatiCache, phase);
            var dialog = new ContentDialog { Title = "Modifica Fase", Content = dialogControl, PrimaryButtonText = "Applica", CloseButtonText = "Annulla", XamlRoot = XamlRoot };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var updated = dialogControl.GetPhase();
                phase.Titolo = updated.Titolo;
                phase.Descrizione = updated.Descrizione;
                phase.DataInizio = updated.DataInizio;
                phase.DataPrevFine = updated.DataPrevFine;
                phase.Stato = updated.Stato;
                phase.AssegnatoA = updated.AssegnatoA;
            }
        }

        private async void PhasesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PhaseViewModel phase) await OpenPhaseDialog(phase);
        }

        private async void RoadmapItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RoadmapItem item) await OpenPhaseDialog(item.OriginalPhase);
        }

        private void BtnAddPhase_Click(object sender, RoutedEventArgs e)
        {
            var startDate = DateTimeOffset.Now;
            if (Phases.Any() && Phases.Last().DataPrevFine.HasValue) startDate = Phases.Last().DataPrevFine!.Value.AddDays(1);
            Phases.Add(new PhaseViewModel { Id = 0, Titolo = string.Empty, DataInizio = startDate, DataPrevFine = startDate.AddDays(5), Stato = _allStatiCache.FirstOrDefault() });
        }

        private void BtnRemovePhase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is PhaseViewModel p) Phases.Remove(p);
        }

        private async void BtnSendComment_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCommento.Text) || Project == null) return;
            var dto = new { Testo = TxtCommento.Text, UtenteId = _currentUser?.Id ?? 1, Username = _currentUser?.Nome ?? "Utente" };
            try
            {
                var res = await _apiClient.PostAsJsonAsync($"{ApiConfig.BaseUrl}/api/progetti/{Project.Id}/commenti", dto);
                if (res.IsSuccessStatusCode) { TxtCommento.Text = ""; await LoadComments(Project.Id); }
            }
            catch { }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
        private void TxtCommento_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter) BtnSendComment_Click(sender, new RoutedEventArgs()); }
    }
}