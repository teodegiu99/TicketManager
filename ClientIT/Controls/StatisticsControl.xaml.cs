using ClientIT.Helpers;
using ClientIT.Models;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.UI;

namespace ClientIT.Controls
{
    public sealed partial class StatisticsControl : UserControl, INotifyPropertyChanged
    {
        private HttpClient _apiClient;
        private string _apiBaseUrl = "http://localhost:5210";
        private List<TicketViewModel> _cachedAllTickets = new();

        // --- PROPERTIES REAL TIME ---
        private int _countOpen;
        private int _countInProgress;
        private int _countClosed;
        private bool _isLoading;

        private ISeries[] _urgencySeries = Array.Empty<ISeries>();
        private ISeries[] _typeSeries = Array.Empty<ISeries>();
        private ISeries[] _statusSeries = Array.Empty<ISeries>();

        // --- PROPERTIES REPORT ---
        private string _avgCloseTime = "N/D";
        private string _urgencyChangedRate = "0%";

        private ISeries[] _reportUrgencySeries = Array.Empty<ISeries>();
        private ISeries[] _reportSedeSeries = Array.Empty<ISeries>();
        private ISeries[] _reportTypeSeries = Array.Empty<ISeries>();
        private ISeries[] _reportUserSeries = Array.Empty<ISeries>();

        private ICartesianAxis[] _userXAxes = Array.Empty<ICartesianAxis>();
        private ICartesianAxis[] _userYAxes = Array.Empty<ICartesianAxis>();

        // --- BINDING PROPERTIES ---
        public int CountOpen { get => _countOpen; set { _countOpen = value; OnPropertyChanged(); } }
        public int CountInProgress { get => _countInProgress; set { _countInProgress = value; OnPropertyChanged(); } }
        public int CountClosed { get => _countClosed; set { _countClosed = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        public ISeries[] UrgencySeries { get => _urgencySeries; set { _urgencySeries = value; OnPropertyChanged(); } }
        public ISeries[] TypeSeries { get => _typeSeries; set { _typeSeries = value; OnPropertyChanged(); } }
        public ISeries[] ColorSeries { get => _statusSeries; set { _statusSeries = value; OnPropertyChanged(); } }

        public string AvgCloseTime { get => _avgCloseTime; set { _avgCloseTime = value; OnPropertyChanged(); } }
        public string UrgencyChangedRate { get => _urgencyChangedRate; set { _urgencyChangedRate = value; OnPropertyChanged(); } }

        public ISeries[] ReportUrgencySeries { get => _reportUrgencySeries; set { _reportUrgencySeries = value; OnPropertyChanged(); } }
        public ISeries[] ReportSedeSeries { get => _reportSedeSeries; set { _reportSedeSeries = value; OnPropertyChanged(); } }
        public ISeries[] ReportTypeSeries { get => _reportTypeSeries; set { _reportTypeSeries = value; OnPropertyChanged(); } }
        public ISeries[] ReportUserSeries { get => _reportUserSeries; set { _reportUserSeries = value; OnPropertyChanged(); } }

        public ICartesianAxis[] UserXAxes { get => _userXAxes; set { _userXAxes = value; OnPropertyChanged(); } }
        public ICartesianAxis[] UserYAxes { get => _userYAxes; set { _userYAxes = value; OnPropertyChanged(); } }

        public SolidColorPaint LegendTextPaint { get; set; } = new SolidColorPaint(SKColors.Gray);

        public StatisticsControl()
        {
            this.InitializeComponent();
            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (s, c, ch, e) => true };
            _apiClient = new HttpClient(handler);

            UserXAxes = new ICartesianAxis[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
            UserYAxes = new ICartesianAxis[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } };

            this.Loaded += StatisticsControl_Loaded;
        }

        private async void StatisticsControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DateStart.Date == null) DateStart.Date = DateTimeOffset.Now.AddYears(-10);
            if (DateEnd.Date == null) DateEnd.Date = DateTimeOffset.Now;

            await LoadStats();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadStats();

        private void BtnFilter_Click(object sender, RoutedEventArgs e) => ProcessReportData(_cachedAllTickets);

        public async Task LoadStats()
        {
            IsLoading = true;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                string url = $"{_apiBaseUrl}/api/tickets/all?includeAll=true&t={DateTime.Now.Ticks}";

                var allTickets = await _apiClient.GetFromJsonAsync<List<TicketViewModel>>(url, options);

                if (allTickets != null)
                {
                    _cachedAllTickets = allTickets;

                    // --- DEBUG FONDAMENTALE ---
                    int totalTickets = allTickets.Count;
                    int closedTickets = allTickets.Count(t => t.StatoId == 3);
                    int nullStato = allTickets.Count(t => t.StatoId == 0);

                    System.Diagnostics.Debug.WriteLine($"[STATS DEBUG] Totali: {totalTickets}");
                    System.Diagnostics.Debug.WriteLine($"[STATS DEBUG] Terminati (Id=3): {closedTickets}");
                    System.Diagnostics.Debug.WriteLine($"[STATS DEBUG] Stato 0 (Errore?): {nullStato}");

                    if (closedTickets == 0 && totalTickets > 0)
                    {
                        // Se qui dice 0, il problema è che i ticket non hanno StatoId=3 nel JSON ricevuto
                        System.Diagnostics.Debug.WriteLine("[STATS DEBUG] ATTENZIONE: Nessun ticket risulta chiuso nel Client!");
                    }
                    // 1. Real Time (Solo Attivi: stato != 3)
                    var activeTickets = allTickets.Where(t => t.StatoId != 3).ToList();
                    ProcessCounters(allTickets);
                    UrgencySeries = CreatePieSeries(activeTickets.GroupBy(t => t.UrgenzaNome));
                    TypeSeries = CreateTypeSeries(activeTickets); // Usa metodo specifico con colori
                    ColorSeries = CreateColorSeries(activeTickets);

                    // 2. Report (Storico - Solo Terminati)
                    ProcessReportData(allTickets);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Err Stats: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ProcessReportData(List<TicketViewModel> allTickets)
        {
            if (DateStart.Date == null || DateEnd.Date == null) return;

            DateTime start = DateStart.Date.Value.DateTime.Date;
            DateTime end = DateEnd.Date.Value.DateTime.Date.AddDays(1).AddSeconds(-1);

            // FILTRO: Solo ticket TERMINATI che rientrano nel range di Data Chiusura
            var filtered = allTickets
                .Where(t => {
                    if (t.StatoId != 3) return false;
                    // Fallback su DataCreazione se DataChiusura è null (per vecchi ticket)
                    DateTime refDate = t.DataChiusura.HasValue ? t.DataChiusura.Value.ToLocalTime() : t.DataCreazione.ToLocalTime();
                    return refDate >= start && refDate <= end;
                })
                .ToList();

            // --- 1. Grafici Torta Semplici ---
            ReportUrgencySeries = CreatePieSeries(filtered.GroupBy(t => t.UrgenzaNome));
            ReportSedeSeries = CreatePieSeries(filtered.GroupBy(t => t.SedeNome));

            // --- 2. Grafico Tipologia (CORRETTO: Usa CreateTypeSeries per i colori) ---
            ReportTypeSeries = CreateTypeSeries(filtered);

            // --- 3. Grafico Utenti (CORRETTO: Include anche 'Non assegnato') ---
            var userGroup = filtered
                .GroupBy(t => string.IsNullOrEmpty(t.AssegnatoaNome) ? "Non assegnato" : t.AssegnatoaNome)
                .OrderByDescending(g => g.Count())
                .ToList();

            ReportUserSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "Ticket Chiusi",
                    Values = userGroup.Select(g => g.Count()).ToArray(),
                    Fill = new SolidColorPaint(SKColors.DodgerBlue),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
                }
            };

            UserXAxes = new ICartesianAxis[]
            {
                new Axis { Labels = userGroup.Select(g => g.Key).ToList(), LabelsPaint = new SolidColorPaint(SKColors.Gray), LabelsRotation = 15 }
            };

            // --- KPI ---
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

        // --- HELPERS ---

        private ISeries[] CreatePieSeries(IEnumerable<IGrouping<string, TicketViewModel>> groups)
        {
            var list = new List<ISeries>();
            foreach (var g in groups)
            {
                list.Add(new PieSeries<int>
                {
                    Values = new[] { g.Count() },
                    Name = string.IsNullOrEmpty(g.Key) ? "N/D" : g.Key,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}"
                });
            }
            return list.ToArray();
        }

        // Helper specifico per Tipologia che gestisce i COLORI PERSONALIZZATI
        private ISeries[] CreateTypeSeries(List<TicketViewModel> tickets)
        {
            var grouped = tickets.GroupBy(t => new { t.TipologiaNome, t.TipologiaColore });
            var list = new List<ISeries>();
            foreach (var g in grouped)
            {
                list.Add(new PieSeries<int>
                {
                    Values = new[] { g.Count() },
                    Name = g.Key.TipologiaNome,
                    Fill = new SolidColorPaint(GetSkColor(g.Key.TipologiaColore)),
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = p => $"{p.Coordinate.PrimaryValue}"
                });
            }
            return list.ToArray();
        }

        private ISeries[] CreateColorSeries(List<TicketViewModel> tickets)
        {
            int g = 0, y = 0, r = 0;
            foreach (var t in tickets) { var c = t.StatusBorderBrush.Color; if (IsCol(c, Colors.LimeGreen)) g++; else if (IsCol(c, Colors.Orange)) y++; else if (IsCol(c, Colors.Red)) r++; }
            var list = new List<ISeries>();
            if (g > 0) list.Add(new PieSeries<int> { Values = new[] { g }, Name = "Nei tempi", Fill = new SolidColorPaint(SKColors.LimeGreen) });
            if (y > 0) list.Add(new PieSeries<int> { Values = new[] { y }, Name = "In scadenza", Fill = new SolidColorPaint(SKColors.Orange) });
            if (r > 0) list.Add(new PieSeries<int> { Values = new[] { r }, Name = "Scaduti", Fill = new SolidColorPaint(SKColors.Red) });
            return list.ToArray();
        }

        private void ProcessCounters(List<TicketViewModel> t)
        {
            CountOpen = t.Count(x => x.StatoId == 1);
            CountInProgress = t.Count(x => x.StatoId == 2);
            CountClosed = t.Count(x => x.StatoId == 3);
        }

        private SKColor GetSkColor(string? s)
        {
            if (string.IsNullOrEmpty(s)) return SKColors.Gray;
            try
            {
                if (s.StartsWith("#")) return SKColor.Parse(s);
                var f = typeof(SKColors).GetField(s, BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
                return f != null ? (SKColor)f.GetValue(null)! : SKColors.Gray;
            }
            catch { return SKColors.Gray; }
        }

        private bool IsCol(Color c1, Color c2) => c1.A == c2.A && c1.R == c2.R && c1.G == c2.G && c1.B == c2.B;

        // Metodo helper aggiunto per compatibilità
        private bool IsRed(Color c) => c.R == 255 && c.G == 0 && c.B == 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}