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
using System.Collections.ObjectModel;
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
        private List<TicketViewModel> _cachedAllTickets = new();
        private List<ItUtente> _cachedItUsers = new(); // Lista utenti IT per il confronto
        private readonly Random _random = new Random();

        // --- CAMPI PRIVATI ---
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

        private int _countOpen;
        private int _countInProgress;
        private int _countClosed;
        private bool _isLoading;
        private string _avgCloseTime = "N/D";
        private string _urgencyChangedRate = "0%";

        // --- CAMPI UTENTE ---
        private Visibility _userStatsVisible = Visibility.Collapsed;
        private int _userOpenTicketsCount;
        private int _userUrgencyChangedCount;
        private IEnumerable<ISeries> _userTypeSeries;
        private IEnumerable<TicketViewModel> _userTicketList;

        // --- PROPRIETÀ BINDING ---
        public IEnumerable<ISeries> UrgencySeries { get => _urgencySeries; set { _urgencySeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> TypeSeries { get => _typeSeries; set { _typeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ColorSeries { get => _colorSeries; set { _colorSeries = value; OnPropertyChanged(); } }

        public IEnumerable<ISeries> ReportUrgencySeries { get => _reportUrgencySeries; set { _reportUrgencySeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportSedeSeries { get => _reportSedeSeries; set { _reportSedeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportTypeSeries { get => _reportTypeSeries; set { _reportTypeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportUserSeries { get => _reportUserSeries; set { _reportUserSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportCreatorSeries { get => _reportCreatorSeries; set { _reportCreatorSeries = value; OnPropertyChanged(); } }

        public IEnumerable<ICartesianAxis> UserXAxes { get => _userXAxes; set { _userXAxes = value; OnPropertyChanged(); } }
        public IEnumerable<ICartesianAxis> UserYAxes { get => _userYAxes; set { _userYAxes = value; OnPropertyChanged(); } }

        public int CountOpen { get => _countOpen; set { _countOpen = value; OnPropertyChanged(); } }
        public int CountInProgress { get => _countInProgress; set { _countInProgress = value; OnPropertyChanged(); } }
        public int CountClosed { get => _countClosed; set { _countClosed = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
        public string AvgCloseTime { get => _avgCloseTime; set { _avgCloseTime = value; OnPropertyChanged(); } }
        public string UrgencyChangedRate { get => _urgencyChangedRate; set { _urgencyChangedRate = value; OnPropertyChanged(); } }

        public Visibility UserStatsVisible { get => _userStatsVisible; set { _userStatsVisible = value; OnPropertyChanged(); } }
        public int UserOpenTicketsCount { get => _userOpenTicketsCount; set { _userOpenTicketsCount = value; OnPropertyChanged(); } }
        public int UserUrgencyChangedCount { get => _userUrgencyChangedCount; set { _userUrgencyChangedCount = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> UserTypeSeries { get => _userTypeSeries; set { _userTypeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<TicketViewModel> UserTicketList { get => _userTicketList; set { _userTicketList = value; OnPropertyChanged(); } }

        public StatisticsControl()
        {
            this.InitializeComponent();
            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (s, c, ch, e) => true };
            _apiClient = new HttpClient(handler);

            UserXAxes = new List<Axis> { new Axis { LabelsRotation = 15, LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
            UserYAxes = new List<Axis> { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } };

            this.Loaded += StatisticsControl_Loaded;
        }

        private async void StatisticsControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DateStart.Date == null) DateStart.Date = DateTimeOffset.Now.AddYears(-10);
            if (DateEnd.Date == null) DateEnd.Date = DateTimeOffset.Now;

            await LoadStats();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadStats();

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_cachedAllTickets != null && _cachedAllTickets.Any())
            {
                ProcessReportData(_cachedAllTickets);
            }
        }

        private void SearchUser_Click(object sender, RoutedEventArgs e)
        {
            string query = UserSearchBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(query) || _cachedAllTickets == null)
            {
                UserStatsVisible = Visibility.Collapsed;
                return;
            }

            var userTickets = _cachedAllTickets
                .Where(t => t.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.DataCreazione)
                .ToList();

            if (!userTickets.Any())
            {
                UserStatsVisible = Visibility.Collapsed;
                return;
            }

            UserOpenTicketsCount = userTickets.Count(t => t.StatoId != 3);
            UserUrgencyChangedCount = userTickets.Count(t => t.UrgenzaCambiata);
            UserTypeSeries = CreateRandomColorPieSeries(userTickets.GroupBy(t => t.TipologiaNome));
            UserTicketList = userTickets;
            UserStatsVisible = Visibility.Visible;
        }

        public async Task LoadStats()
        {
            this.DispatcherQueue.TryEnqueue(() => IsLoading = true);
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // 1. Carica Utenti IT se non già caricati
                if (_cachedItUsers.Count == 0)
                {
                    try
                    {
                        var itUsers = await _apiClient.GetFromJsonAsync<List<ItUtente>>($"{_apiBaseUrl}/api/auth/users", options);
                        if (itUsers != null) _cachedItUsers = itUsers;

                        // Debug per vedere chi ha caricato
                        foreach (var u in _cachedItUsers)
                            System.Diagnostics.Debug.WriteLine($"[IT USER LOADED] ID: {u.Id}, User: {u.UsernameAd}, Nome: {u.Nome}");
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Err Loading IT Users: " + ex.Message); }
                }

                // 2. Carica Ticket
                string url = $"{_apiBaseUrl}/api/tickets/all?includeAll=true&t={DateTime.Now.Ticks}";
                var allTickets = await _apiClient.GetFromJsonAsync<List<TicketViewModel>>(url, options);

                if (allTickets != null)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        _cachedAllTickets = allTickets;

                        int closedTickets = allTickets.Count(t => t.StatoId == 3);
                        System.Diagnostics.Debug.WriteLine($"[STATS DEBUG] Totali: {allTickets.Count}, Chiusi (Id=3): {closedTickets}");

                        var activeTickets = allTickets.Where(t => t.StatoId != 3).ToList();
                        ProcessCounters(allTickets);

                        UrgencySeries = CreateRandomColorPieSeries(activeTickets.GroupBy(t => t.UrgenzaNome));
                        TypeSeries = CreateRandomColorPieSeries(activeTickets.GroupBy(t => t.TipologiaNome));
                        ColorSeries = CreateColorSeries(activeTickets);

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

        private void ProcessReportData(List<TicketViewModel> allTickets)
        {
            if (DateStart.Date == null || DateEnd.Date == null) return;

            DateTime start = DateStart.Date.Value.DateTime.Date;
            DateTime end = DateEnd.Date.Value.DateTime.Date.AddDays(2).AddSeconds(-1);

            var filtered = allTickets
                .Where(t => {
                    if (t.StatoId != 3) return false;
                    DateTime refDate = t.DataChiusura.HasValue ? t.DataChiusura.Value.ToLocalTime() : t.DataCreazione.ToLocalTime();
                    return refDate >= start && refDate <= end;
                })
                .ToList();

            // Aggiorna Grafici Standard
            ReportUrgencySeries = CreateRandomColorPieSeries(filtered.GroupBy(t => t.UrgenzaNome));
            ReportSedeSeries = CreateRandomColorPieSeries(filtered.GroupBy(t => t.SedeNome));
            ReportTypeSeries = CreateRandomColorPieSeries(filtered.GroupBy(t => t.TipologiaNome));

            // --- LOGICA CED AVANZATA ---
            int countCed = 0;
            int countBehalf = 0;
            int countOwn = 0;

            System.Diagnostics.Debug.WriteLine("--- INIZIO ANALISI TICKET ---");

            foreach (var t in filtered)
            {
                // Controlla se l'utente del ticket è un membro IT
                bool isCed = IsItUser(t.Username);

                if (isCed)
                {
                    countCed++;
                    System.Diagnostics.Debug.WriteLine($"[CED CHECK] Ticket {t.Nticket} di '{t.Username}' -> RICONOSCIUTO COME CED");
                }
                else if (!string.IsNullOrEmpty(t.PerContoDi))
                {
                    countBehalf++;
                    System.Diagnostics.Debug.WriteLine($"[CED CHECK] Ticket {t.Nticket} di '{t.Username}' -> PER CONTO DI ({t.PerContoDi})");
                }
                else
                {
                    countOwn++;
                    System.Diagnostics.Debug.WriteLine($"[CED CHECK] Ticket {t.Nticket} di '{t.Username}' -> PROPRIO");
                }
            }
            System.Diagnostics.Debug.WriteLine("--- FINE ANALISI ---");

            var creatorList = new List<ISeries>();
            if (countOwn > 0) creatorList.Add(new PieSeries<double> { Values = new[] { (double)countOwn }, Name = "Per conto proprio", Fill = new SolidColorPaint(GetRandomColor()) });
            if (countBehalf > 0) creatorList.Add(new PieSeries<double> { Values = new[] { (double)countBehalf }, Name = "Per conto di terzi", Fill = new SolidColorPaint(GetRandomColor()) });
            if (countCed > 0) creatorList.Add(new PieSeries<double> { Values = new[] { (double)countCed }, Name = "Aperti da CED", Fill = new SolidColorPaint(GetRandomColor()) });

            ReportCreatorSeries = creatorList;

            // Aggiorna Grafico Utenti
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

            // KPI
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

        // --- HELPER DI CONFRONTO AVANZATO ---
        private bool IsItUser(string ticketUsername)
        {
            if (string.IsNullOrEmpty(ticketUsername)) return false;

            // 1. Pulizia: Se il ticket ha "DOMAIN\user", proviamo a prendere solo "user"
            string cleanTicketUser = CleanUsername(ticketUsername);

            foreach (var itUser in _cachedItUsers)
            {
                // CONTROLLO A: Match su Username AD (es. "mrossi" == "mrossi")
                if (!string.IsNullOrEmpty(itUser.UsernameAd))
                {
                    string cleanItUser = CleanUsername(itUser.UsernameAd);
                    if (cleanTicketUser.Equals(cleanItUser, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // CONTROLLO B: Match su Nome Completo (RICHIESTO)
                // Se il ticket è stato salvato come "Mario Rossi", lo confrontiamo con it_utenti.NomeCompleto
                if (!string.IsNullOrEmpty(itUser.NomeCompleto))
                {
                    if (ticketUsername.Equals(itUser.NomeCompleto, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // CONTROLLO C: Match su Nome Breve (Fallback esistente)
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
            // Prende solo la parte dopo lo slash (es. "DOMAIN\user" -> "user")
            int slashIndex = fullUsername.LastIndexOf('\\');
            if (slashIndex >= 0 && slashIndex < fullUsername.Length - 1)
                return fullUsername.Substring(slashIndex + 1);
            return fullUsername;
        }

        // --- CREATORI SERIE ---

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