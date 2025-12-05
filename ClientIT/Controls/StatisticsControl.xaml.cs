using ClientIT.Helpers;
using ClientIT.Models;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClientIT.Controls
{
    public sealed partial class StatisticsControl : UserControl, INotifyPropertyChanged
    {
        private HttpClient _apiClient;
        private string _apiBaseUrl = "http://localhost:5210";

        // Cache dati principali
        private List<TicketViewModel> _cachedAllTickets = new();

        // Cache dati per i dropdown (Stati, Utenti, Tipologie, Urgenze)
        // Necessari per il dettaglio modale
        private List<ItUtente> _cachedItUsers = new();
        private List<Stato> _cachedStati = new();
        private List<Tipologia> _cachedTipologie = new();
        private List<Urgenza> _cachedUrgenze = new();
        private bool _referenceDataLoaded = false;

        private readonly Random _random = new Random();

        // --- CAMPI PRIVATI PER I GRAFICI GENERALI ---
        private IEnumerable<ISeries> _urgencySeries;
        private IEnumerable<ISeries> _typeSeries;
        private IEnumerable<ISeries> _colorSeries;

        private IEnumerable<ISeries> _reportUrgencySeries;
        private IEnumerable<ISeries> _reportSedeSeries;
        private IEnumerable<ISeries> _reportTypeSeries;
        private IEnumerable<ISeries> _reportUserSeries;
        private IEnumerable<ISeries> _reportCreatorSeries;

        private IEnumerable<ICartesianAxis> _userXAxes;
        private IEnumerable<ICartesianAxis> _userYAxes;

        // --- CONTATORI GENERALI ---
        private int _countOpen;
        private int _countInProgress;
        private int _countClosed;
        private bool _isLoading;
        private string _avgCloseTime = "N/D";
        private string _urgencyChangedRate = "0%";

        // --- CAMPI PER LA SEZIONE UTENTE (AGGIORNATI) ---
        private Visibility _userStatsVisible = Visibility.Collapsed;
        private IEnumerable<TicketViewModel> _userTicketList;

        // Grafici specifici utente
        private IEnumerable<ISeries> _userTypeSeries;
        private IEnumerable<ISeries> _userUrgencySeries;

        // Dati di dettaglio utente
        private string _userMachineName = "N/D";
        private int _userOwnOpenCount;      // Ticket propri (Aperti/In Corso)
        private int _userOwnClosedCount;    // Ticket propri (Terminati)
        private int _userBehalfOpenCount;   // Ticket per conto di (Aperti/In Corso)
        private int _userBehalfClosedCount; // Ticket per conto di (Terminati)
        private int _userTicketsReceivedCount; // Ticket aperti da altri PER questo utente
        private int _userUrgencyChangedCount;  // Quante volte è stata cambiata l'urgenza

        // =========================================================
        // PROPRIETÀ PUBBLICHE (BINDING)
        // =========================================================

        // Serie Grafici Dashboard
        public IEnumerable<ISeries> UrgencySeries { get => _urgencySeries; set { _urgencySeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> TypeSeries { get => _typeSeries; set { _typeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ColorSeries { get => _colorSeries; set { _colorSeries = value; OnPropertyChanged(); } }

        // Serie Grafici Reportistica
        public IEnumerable<ISeries> ReportUrgencySeries { get => _reportUrgencySeries; set { _reportUrgencySeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportSedeSeries { get => _reportSedeSeries; set { _reportSedeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportTypeSeries { get => _reportTypeSeries; set { _reportTypeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportUserSeries { get => _reportUserSeries; set { _reportUserSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportCreatorSeries { get => _reportCreatorSeries; set { _reportCreatorSeries = value; OnPropertyChanged(); } }

        public IEnumerable<ICartesianAxis> UserXAxes { get => _userXAxes; set { _userXAxes = value; OnPropertyChanged(); } }
        public IEnumerable<ICartesianAxis> UserYAxes { get => _userYAxes; set { _userYAxes = value; OnPropertyChanged(); } }

        // Contatori Dashboard
        public int CountOpen { get => _countOpen; set { _countOpen = value; OnPropertyChanged(); } }
        public int CountInProgress { get => _countInProgress; set { _countInProgress = value; OnPropertyChanged(); } }
        public int CountClosed { get => _countClosed; set { _countClosed = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
        public string AvgCloseTime { get => _avgCloseTime; set { _avgCloseTime = value; OnPropertyChanged(); } }
        public string UrgencyChangedRate { get => _urgencyChangedRate; set { _urgencyChangedRate = value; OnPropertyChanged(); } }

        // Proprietà Sezione Utente
        public Visibility UserStatsVisible { get => _userStatsVisible; set { _userStatsVisible = value; OnPropertyChanged(); } }
        public IEnumerable<TicketViewModel> UserTicketList { get => _userTicketList; set { _userTicketList = value; OnPropertyChanged(); } }

        public IEnumerable<ISeries> UserTypeSeries { get => _userTypeSeries; set { _userTypeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> UserUrgencySeries { get => _userUrgencySeries; set { _userUrgencySeries = value; OnPropertyChanged(); } }

        public string UserMachineName { get => _userMachineName; set { _userMachineName = value; OnPropertyChanged(); } }

        public int UserOwnOpenCount { get => _userOwnOpenCount; set { _userOwnOpenCount = value; OnPropertyChanged(); } }
        public int UserOwnClosedCount { get => _userOwnClosedCount; set { _userOwnClosedCount = value; OnPropertyChanged(); } }

        public int UserBehalfOpenCount { get => _userBehalfOpenCount; set { _userBehalfOpenCount = value; OnPropertyChanged(); } }
        public int UserBehalfClosedCount { get => _userBehalfClosedCount; set { _userBehalfClosedCount = value; OnPropertyChanged(); } }

        public int UserTicketsReceivedCount { get => _userTicketsReceivedCount; set { _userTicketsReceivedCount = value; OnPropertyChanged(); } }
        public int UserUrgencyChangedCount { get => _userUrgencyChangedCount; set { _userUrgencyChangedCount = value; OnPropertyChanged(); } }

        // =========================================================
        // COSTRUTTORE E INIZIALIZZAZIONE
        // =========================================================

        public StatisticsControl()
        {
            this.InitializeComponent();
            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (s, c, ch, e) => true };
            _apiClient = new HttpClient(handler);

            // Configurazione assi per i grafici cartesiani
            UserXAxes = new List<Axis> { new Axis { LabelsRotation = 15, LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
            UserYAxes = new List<Axis> { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } };

            this.Loaded += StatisticsControl_Loaded;
        }

        private async void StatisticsControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Imposta date di default se vuote
            if (DateStart.Date == null) DateStart.Date = DateTimeOffset.Now.AddYears(-10);
            if (DateEnd.Date == null) DateEnd.Date = DateTimeOffset.Now;

            // 1. Carica i dati di riferimento (necessari per il dettaglio modale)
            await LoadReferenceData();

            // 2. Carica le statistiche
            await LoadStats();
        }

        // =========================================================
        // CARICAMENTO DATI
        // =========================================================

        // Metodo per caricare le liste statiche (Stati, Tipologie, ecc.)
        private async Task LoadReferenceData()
        {
            if (_referenceDataLoaded) return;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var tStati = _apiClient.GetFromJsonAsync<List<Stato>>($"{_apiBaseUrl}/api/tickets/stati", options);
                var tTipologie = _apiClient.GetFromJsonAsync<List<Tipologia>>($"{_apiBaseUrl}/api/tickets/tipologie", options);
                var tUrgenze = _apiClient.GetFromJsonAsync<List<Urgenza>>($"{_apiBaseUrl}/api/tickets/urgenze", options);
                var tUtenti = _apiClient.GetFromJsonAsync<List<ItUtente>>($"{_apiBaseUrl}/api/auth/users", options);

                await Task.WhenAll(tStati, tTipologie, tUrgenze, tUtenti);

                if (tStati.Result != null) _cachedStati = tStati.Result;
                if (tTipologie.Result != null) _cachedTipologie = tTipologie.Result;
                if (tUrgenze.Result != null) _cachedUrgenze = tUrgenze.Result;
                if (tUtenti.Result != null) _cachedItUsers = tUtenti.Result;

                // Aggiungiamo "Non assegnato" in testa alla lista utenti per le combo
                if (!_cachedItUsers.Any(u => u.Id == 0))
                {
                    _cachedItUsers.Insert(0, ItUtente.NonAssegnato ?? new ItUtente { Id = 0, UsernameAd = "Non assegnato", Nome = "Non assegnato" });
                }

                _referenceDataLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading ref data: {ex.Message}");
            }
        }

        public async Task LoadStats()
        {
            this.DispatcherQueue.TryEnqueue(() => IsLoading = true);
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Fallback: se LoadReferenceData non è stato chiamato o è fallito, riprova a caricare gli utenti IT
                if (_cachedItUsers.Count == 0) await LoadReferenceData();

                // Carica tutti i ticket
                string url = $"{_apiBaseUrl}/api/tickets/all?includeAll=true&t={DateTime.Now.Ticks}";
                var allTickets = await _apiClient.GetFromJsonAsync<List<TicketViewModel>>(url, options);

                if (allTickets != null)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        _cachedAllTickets = allTickets;

                        // Calcola statistiche generali
                        var activeTickets = allTickets.Where(t => t.StatoId != 3).ToList();
                        ProcessCounters(allTickets);

                        // Aggiorna grafici torta generali
                        UrgencySeries = CreateRandomColorPieSeries(activeTickets.GroupBy(t => t.UrgenzaNome));
                        TypeSeries = CreateRandomColorPieSeries(activeTickets.GroupBy(t => t.TipologiaNome));
                        ColorSeries = CreateColorSeries(activeTickets);

                        // Aggiorna grafici reportistica
                        ProcessReportData(allTickets);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Err Stats: {ex.Message}");
            }
            finally
            {
                this.DispatcherQueue.TryEnqueue(() => IsLoading = false);
            }
        }

        // =========================================================
        // GESTIONE EVENTI UTENTE (RICERCA E CLICK)
        // =========================================================

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadStats();

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_cachedAllTickets != null && _cachedAllTickets.Any())
            {
                ProcessReportData(_cachedAllTickets);
            }
        }

        // LOGICA RICERCA UTENTE (COMPLETA E AGGIORNATA)
        private void SearchUser_Click(object sender, RoutedEventArgs e)
        {
            string query = UserSearchBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(query) || _cachedAllTickets == null)
            {
                UserStatsVisible = Visibility.Collapsed;
                return;
            }

            // 1. Cerca i ticket creati dall'utente (Username)
            var userTickets = _cachedAllTickets
                .Where(t => t.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.DataCreazione)
                .ToList();

            if (!userTickets.Any())
            {
                UserStatsVisible = Visibility.Collapsed;
                return;
            }

            // 2. Cerca i ticket aperti da altri PER questo utente (PerContoDi)
            var receivedTickets = _cachedAllTickets
                .Where(t => !string.IsNullOrEmpty(t.PerContoDi) &&
                            t.PerContoDi.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            UserTicketsReceivedCount = receivedTickets.Count;

            // 3. Recupera il nome macchina più recente
            var lastTicketWithMachine = userTickets.FirstOrDefault(t => !string.IsNullOrEmpty(t.Macchina));
            UserMachineName = lastTicketWithMachine != null ? lastTicketWithMachine.Macchina : "N/D";

            // 4. Calcola i contatori specifici (Propri vs Per Conto Di)
            // Nota: StatoId 3 = Terminato

            // Propri (PerContoDi vuoto)
            UserOwnOpenCount = userTickets.Count(t => string.IsNullOrEmpty(t.PerContoDi) && t.StatoId != 3);
            UserOwnClosedCount = userTickets.Count(t => string.IsNullOrEmpty(t.PerContoDi) && t.StatoId == 3);

            // Per Conto Di (PerContoDi pieno)
            UserBehalfOpenCount = userTickets.Count(t => !string.IsNullOrEmpty(t.PerContoDi) && t.StatoId != 3);
            UserBehalfClosedCount = userTickets.Count(t => !string.IsNullOrEmpty(t.PerContoDi) && t.StatoId == 3);

            // Altri contatori
            UserUrgencyChangedCount = userTickets.Count(t => t.UrgenzaCambiata);

            // 5. Aggiorna liste e grafici
            UserTicketList = userTickets;
            UserTypeSeries = CreateRandomColorPieSeries(userTickets.GroupBy(t => t.TipologiaNome));
            UserUrgencySeries = CreateRandomColorPieSeries(userTickets.GroupBy(t => t.UrgenzaNome));

            UserStatsVisible = Visibility.Visible;
        }

        // GESTIONE CLICK SU LISTA TICKET -> APERTURA MODALE
        private async void UserTicketListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TicketViewModel ticket)
            {
                // Crea il controllo di dettaglio passando i dati necessari
                var detailControl = new TicketDetailControl
                {
                    ViewModel = ticket,
                    StatoOptions = _cachedStati,
                    AssigneeOptions = _cachedItUsers,
                    TipologiaOptions = _cachedTipologie,
                    UrgenzaOptions = _cachedUrgenze
                };

                // Collega gli eventi per il salvataggio automatico
                detailControl.TicketStateChanged += async (s, args) => await SaveFullTicketStateAsync(args.Nticket, detailControl.ViewModel);
                detailControl.TicketAssigneeChanged += async (s, args) => await SaveFullTicketStateAsync(args.Nticket, detailControl.ViewModel);
                detailControl.TicketPropertyChanged += async (s, args) => await SaveFullTicketStateAsync(args.Nticket, detailControl.ViewModel);

                // Mostra il dialogo modale
                var dialog = new ContentDialog
                {
                    Title = $"Dettaglio Ticket #{ticket.Nticket}",
                    Content = detailControl,
                    CloseButtonText = "Chiudi",
                    XamlRoot = this.XamlRoot,

                    // Impostazioni larghezza aumentate
                    Width = 1200,       // Larghezza desiderata (aumentata da 900)
                    MinWidth = 1200,     // Larghezza minima
                    MaxWidth = 2000     // Importante: alza il limite massimo per permettere l'allargamento
                };

                await dialog.ShowAsync();

                // Al termine, ricarica le statistiche per riflettere eventuali cambiamenti
                await LoadStats();

                // Se la vista utente era aperta, aggiorna anche quella rieseguendo la ricerca
                if (UserStatsVisible == Visibility.Visible)
                {
                    SearchUser_Click(null, null);
                }
            }
        }

        // Metodo helper per salvare i cambiamenti dal modale
        private async Task SaveFullTicketStateAsync(int nticket, TicketViewModel ticket)
        {
            try
            {
                string url = $"{_apiBaseUrl}/api/tickets/{nticket}/update";
                var request = new
                {
                    StatoId = ticket.StatoId,
                    AssegnatoaId = ticket.AssegnatoaId == 0 ? null : ticket.AssegnatoaId,
                    UrgenzaId = ticket.UrgenzaId,
                    TipologiaId = ticket.TipologiaId,
                    Note = ticket.Note
                };

                var response = await _apiClient.PutAsJsonAsync(url, request);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore salvataggio: {ex.Message}");
            }
        }

        // =========================================================
        // ELABORAZIONE REPORT E HELPER
        // =========================================================

        private void ProcessReportData(List<TicketViewModel> allTickets)
        {
            if (DateStart.Date == null || DateEnd.Date == null) return;

            DateTime start = DateStart.Date.Value.DateTime.Date;
            DateTime end = DateEnd.Date.Value.DateTime.Date.AddDays(2).AddSeconds(-1);

            // Filtra solo i ticket terminati nel periodo selezionato
            var filtered = allTickets
                .Where(t => {
                    if (t.StatoId != 3) return false;
                    DateTime refDate = t.DataChiusura.HasValue ? t.DataChiusura.Value.ToLocalTime() : t.DataCreazione.ToLocalTime();
                    return refDate >= start && refDate <= end;
                })
                .ToList();

            // Aggiorna Grafici Standard Reportistica
            ReportUrgencySeries = CreateRandomColorPieSeries(filtered.GroupBy(t => t.UrgenzaNome));
            ReportSedeSeries = CreateRandomColorPieSeries(filtered.GroupBy(t => t.SedeNome));
            ReportTypeSeries = CreateRandomColorPieSeries(filtered.GroupBy(t => t.TipologiaNome));

            // LOGICA CED AVANZATA (Chi ha aperto il ticket?)
            int countCed = 0;
            int countBehalf = 0;
            int countOwn = 0;

            foreach (var t in filtered)
            {
                // Controlla se l'utente del ticket è un membro IT
                bool isCed = IsItUser(t.Username);

                if (isCed)
                {
                    countCed++;
                }
                else if (!string.IsNullOrEmpty(t.PerContoDi))
                {
                    countBehalf++;
                }
                else
                {
                    countOwn++;
                }
            }

            var creatorList = new List<ISeries>();
            if (countOwn > 0) creatorList.Add(new PieSeries<double> { Values = new[] { (double)countOwn }, Name = "Per conto proprio", Fill = new SolidColorPaint(GetRandomColor()) });
            if (countBehalf > 0) creatorList.Add(new PieSeries<double> { Values = new[] { (double)countBehalf }, Name = "Per conto di terzi", Fill = new SolidColorPaint(GetRandomColor()) });
            if (countCed > 0) creatorList.Add(new PieSeries<double> { Values = new[] { (double)countCed }, Name = "Aperti da CED", Fill = new SolidColorPaint(GetRandomColor()) });

            ReportCreatorSeries = creatorList;

            // Aggiorna Grafico Utenti (chi ne apre di più)
            var userGroup = filtered
                .GroupBy(t => string.IsNullOrEmpty(t.AssegnatoaNome) ? "Non assegnato" : t.AssegnatoaNome)
                .OrderByDescending(g => g.Count())
                .ToList();

            UserXAxes = new List<Axis>
            {
                new Axis
                {
                    Labels = userGroup.Select(g => g.Key).ToList(),
                    LabelsRotation = 15,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray)
                }
            };

            var barColor = GetRandomColor();
            ReportUserSeries = new List<ISeries>
            {
                new ColumnSeries<double>
                {
                    Name = "Ticket Chiusi",
                    Values = userGroup.Select(g => (double)g.Count()).ToArray(),
                    Fill = new SolidColorPaint(barColor),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
                }
            };

            // KPI Generali
            int total = filtered.Count;
            int changed = filtered.Count(t => t.UrgenzaCambiata);
            UrgencyChangedRate = total > 0 ? $"{(double)changed / total:P0}" : "0%";

            var closedWithDate = filtered.Where(t => t.DataChiusura.HasValue).ToList();
            if (closedWithDate.Any())
            {
                double totalHours = closedWithDate.Sum(t => BusinessTimeCalculator.GetBusinessHoursElapsed(t.DataCreazione, t.DataChiusura.Value));
                AvgCloseTime = $"{totalHours / closedWithDate.Count:F1} h";
            }
            else
            {
                AvgCloseTime = "N/D";
            }
        }

        // =========================================================
        // HELPER E FUNZIONI DI SUPPORTO
        // =========================================================

        private bool IsItUser(string ticketUsername)
        {
            if (string.IsNullOrEmpty(ticketUsername)) return false;

            string cleanTicketUser = CleanUsername(ticketUsername);

            foreach (var itUser in _cachedItUsers)
            {
                // Controllo su Username AD
                if (!string.IsNullOrEmpty(itUser.UsernameAd))
                {
                    string cleanItUser = CleanUsername(itUser.UsernameAd);
                    if (cleanTicketUser.Equals(cleanItUser, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // Controllo su Nome Completo (Prioritario)
                if (!string.IsNullOrEmpty(itUser.NomeCompleto))
                {
                    if (ticketUsername.Equals(itUser.NomeCompleto, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // Fallback su Nome semplice
                if (!string.IsNullOrEmpty(itUser.Nome))
                {
                    if (ticketUsername.Equals(itUser.Nome, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        private string CleanUsername(string fullUsername)
        {
            if (string.IsNullOrEmpty(fullUsername)) return "";
            int slashIndex = fullUsername.LastIndexOf('\\');
            if (slashIndex >= 0 && slashIndex < fullUsername.Length - 1)
                return fullUsername.Substring(slashIndex + 1);
            return fullUsername;
        }

        private IEnumerable<ISeries> CreateRandomColorPieSeries(IEnumerable<IGrouping<string, TicketViewModel>> groups)
        {
            var list = new List<ISeries>();
            foreach (var g in groups)
            {
                var randomColor = GetRandomColor();
                list.Add(new PieSeries<double>
                {
                    Values = new[] { (double)g.Count() },
                    Name = string.IsNullOrEmpty(g.Key) ? "N/D" : g.Key,
                    Fill = new SolidColorPaint(randomColor),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}",
                    DataLabelsPaint = new SolidColorPaint(SKColors.White)
                });
            }
            return list.ToArray();
        }

        private IEnumerable<ISeries> CreateColorSeries(List<TicketViewModel> tickets)
        {
            int g = 0, y = 0, r = 0;
            foreach (var t in tickets)
            {
                var c = t.StatusBorderBrush.Color;
                if (IsCol(c, Microsoft.UI.Colors.LimeGreen)) g++;
                else if (IsCol(c, Microsoft.UI.Colors.Orange)) y++;
                else if (IsCol(c, Microsoft.UI.Colors.Red)) r++;
            }

            var list = new List<ISeries>();
            if (g > 0) list.Add(new PieSeries<double> { Values = new[] { (double)g }, Name = "Nei tempi", Fill = new SolidColorPaint(SKColors.LimeGreen) });
            if (y > 0) list.Add(new PieSeries<double> { Values = new[] { (double)y }, Name = "In scadenza", Fill = new SolidColorPaint(SKColors.Orange) });
            if (r > 0) list.Add(new PieSeries<double> { Values = new[] { (double)r }, Name = "Scaduti", Fill = new SolidColorPaint(SKColors.Red) });
            return list;
        }

        private void ProcessCounters(List<TicketViewModel> t)
        {
            CountOpen = t.Count(x => x.StatoId == 1);
            CountInProgress = t.Count(x => x.StatoId == 2);
            CountClosed = t.Count(x => x.StatoId == 3);
        }

        private SKColor GetRandomColor()
        {
            byte[] bytes = new byte[3];
            _random.NextBytes(bytes);
            return new SKColor(bytes[0], bytes[1], bytes[2], 255);
        }

        private bool IsCol(Windows.UI.Color c1, Windows.UI.Color c2)
        {
            return c1.A == c2.A && c1.R == c2.R && c1.G == c2.G && c1.B == c2.B;
        }

        public static string FormatDate(DateTime d) => d.ToString("dd/MM/yyyy");

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}