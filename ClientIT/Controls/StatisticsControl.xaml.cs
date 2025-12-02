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
        private List<TicketViewModel> _cachedAllTickets = new();

        // --- CAMPI PRIVATI (Backing Fields) ---
        private IEnumerable<ISeries> _urgencySeries;
        private IEnumerable<ISeries> _typeSeries;
        private IEnumerable<ISeries> _colorSeries;

        private IEnumerable<ISeries> _reportUrgencySeries;
        private IEnumerable<ISeries> _reportSedeSeries;
        private IEnumerable<ISeries> _reportTypeSeries;
        private IEnumerable<ISeries> _reportUserSeries;

        private IEnumerable<ICartesianAxis> _userXAxes;
        private IEnumerable<ICartesianAxis> _userYAxes;

        private int _countOpen;
        private int _countInProgress;
        private int _countClosed;
        private bool _isLoading;
        private string _avgCloseTime = "N/D";
        private string _urgencyChangedRate = "0%";

        // --- PROPRIETÀ BINDING (Sostituzione diretta per forzare refresh) ---
        public IEnumerable<ISeries> UrgencySeries { get => _urgencySeries; set { _urgencySeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> TypeSeries { get => _typeSeries; set { _typeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ColorSeries { get => _colorSeries; set { _colorSeries = value; OnPropertyChanged(); } }

        public IEnumerable<ISeries> ReportUrgencySeries { get => _reportUrgencySeries; set { _reportUrgencySeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportSedeSeries { get => _reportSedeSeries; set { _reportSedeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportTypeSeries { get => _reportTypeSeries; set { _reportTypeSeries = value; OnPropertyChanged(); } }
        public IEnumerable<ISeries> ReportUserSeries { get => _reportUserSeries; set { _reportUserSeries = value; OnPropertyChanged(); } }

        public IEnumerable<ICartesianAxis> UserXAxes { get => _userXAxes; set { _userXAxes = value; OnPropertyChanged(); } }
        public IEnumerable<ICartesianAxis> UserYAxes { get => _userYAxes; set { _userYAxes = value; OnPropertyChanged(); } }

        public int CountOpen { get => _countOpen; set { _countOpen = value; OnPropertyChanged(); } }
        public int CountInProgress { get => _countInProgress; set { _countInProgress = value; OnPropertyChanged(); } }
        public int CountClosed { get => _countClosed; set { _countClosed = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
        public string AvgCloseTime { get => _avgCloseTime; set { _avgCloseTime = value; OnPropertyChanged(); } }
        public string UrgencyChangedRate { get => _urgencyChangedRate; set { _urgencyChangedRate = value; OnPropertyChanged(); } }

        public StatisticsControl()
        {
            this.InitializeComponent();
            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (s, c, ch, e) => true };
            _apiClient = new HttpClient(handler);

            // Assi di default
            UserXAxes = new List<Axis> { new Axis { LabelsRotation = 15 } };
            UserYAxes = new List<Axis> { new Axis() };

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

        public async Task LoadStats()
        {
            this.DispatcherQueue.TryEnqueue(() => IsLoading = true);
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                string url = $"{_apiBaseUrl}/api/tickets/all?includeAll=true&t={DateTime.Now.Ticks}";

                var allTickets = await _apiClient.GetFromJsonAsync<List<TicketViewModel>>(url, options);

                if (allTickets != null)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        _cachedAllTickets = allTickets;

                        // Debug
                        int closedTickets = allTickets.Count(t => t.StatoId == 3);
                        System.Diagnostics.Debug.WriteLine($"[STATS DEBUG] Totali: {allTickets.Count}, Chiusi (Id=3): {closedTickets}");

                        // 1. Real Time
                        var activeTickets = allTickets.Where(t => t.StatoId != 3).ToList();
                        ProcessCounters(allTickets);

                        // Creazione NUOVE liste (Forza il ridisegno)
                        UrgencySeries = CreatePieSeries(activeTickets.GroupBy(t => t.UrgenzaNome));
                        TypeSeries = CreatePieSeries(activeTickets.GroupBy(t => t.TipologiaNome));
                        ColorSeries = CreateColorSeries(activeTickets);

                        // 2. Report Storico
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

            System.Diagnostics.Debug.WriteLine($"[FILTER DEBUG] Filtrati: {filtered.Count}");

            // ASSEGNAZIONI DIRETTE (Reset Totale Grafici)
            ReportUrgencySeries = CreatePieSeries(filtered.GroupBy(t => t.UrgenzaNome));
            ReportSedeSeries = CreatePieSeries(filtered.GroupBy(t => t.SedeNome));
            ReportTypeSeries = CreatePieSeries(filtered.GroupBy(t => t.TipologiaNome));

            // Utenti (Bar Chart)
            var userGroup = filtered
                .GroupBy(t => string.IsNullOrEmpty(t.AssegnatoaNome) ? "Non assegnato" : t.AssegnatoaNome)
                .OrderByDescending(g => g.Count())
                .ToList();

            // Aggiorna Assi (Creando nuova lista)
            UserXAxes = new List<Axis>
            {
                new Axis
                {
                    Labels = userGroup.Select(g => g.Key).ToList(),
                    LabelsRotation = 15,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray)
                }
            };

            // Aggiorna Serie Barre
            ReportUserSeries = new List<ISeries>
            {
                new ColumnSeries<double> // Usa double per sicurezza
                {
                    Name = "Ticket Chiusi",
                    Values = userGroup.Select(g => (double)g.Count()).ToArray(),
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

        // --- CREATORS ---

        private IEnumerable<ISeries> CreatePieSeries(IEnumerable<IGrouping<string, TicketViewModel>> groups)
        {
            return groups.Select(g => new PieSeries<double>
            {
                Values = new[] { (double)g.Count() },
                Name = string.IsNullOrEmpty(g.Key) ? "N/D" : g.Key,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}"
            }).ToArray();
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
            if (g > 0) list.Add(new PieSeries<double> { Values = new[] { (double)g }, Name = "Nei tempi" });
            if (y > 0) list.Add(new PieSeries<double> { Values = new[] { (double)y }, Name = "In scadenza" });
            if (r > 0) list.Add(new PieSeries<double> { Values = new[] { (double)r }, Name = "Scaduti" });
            return list;
        }

        private void ProcessCounters(List<TicketViewModel> t)
        {
            CountOpen = t.Count(x => x.StatoId == 1);
            CountInProgress = t.Count(x => x.StatoId == 2);
            CountClosed = t.Count(x => x.StatoId == 3);
        }

        private bool IsCol(Windows.UI.Color c1, Windows.UI.Color c2)
        {
            return c1.A == c2.A && c1.R == c2.R && c1.G == c2.G && c1.B == c2.B;
        }

        // --- IMPLEMENTAZIONE INotifyPropertyChanged ---
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}