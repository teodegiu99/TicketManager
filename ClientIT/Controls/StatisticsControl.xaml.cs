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
        private string _assignmentRate = "0%";

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
        public ISeries[] StatusSeries { get => _statusSeries; set { _statusSeries = value; OnPropertyChanged(); } }

        public string AvgCloseTime { get => _avgCloseTime; set { _avgCloseTime = value; OnPropertyChanged(); } }
        public string AssignmentRate { get => _assignmentRate; set { _assignmentRate = value; OnPropertyChanged(); } }

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
            // Imposta un range di default ampio (1 Anno) per vedere subito i dati storici
            if (DateStart.Date == null) DateStart.Date = DateTimeOffset.Now.AddYears(-1);
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
                string url = $"{_apiBaseUrl}/api/tickets/all?includeAll=true&t={DateTime.Now.Ticks}";
                var response = await _apiClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var allTickets = JsonSerializer.Deserialize<List<TicketViewModel>>(json, options);

                if (allTickets != null)
                {
                    _cachedAllTickets = allTickets;

                    // 1. REAL TIME (Tutti TRANNE i Terminati)
                    // ID 3 = Terminato
                    var activeTickets = allTickets.Where(t => t.StatoId != 3).ToList();

                    ProcessCounters(allTickets);

                    UrgencySeries = CreatePieSeries(activeTickets.GroupBy(t => t.UrgenzaNome));
                    TypeSeries = CreatePieSeries(activeTickets.GroupBy(t => t.TipologiaNome));
                    StatusSeries = CreatePieSeries(activeTickets.GroupBy(t => t.StatoNome));

                    // 2. REPORT STORICO (Solo i Terminati nel periodo)
                    ProcessReportData(allTickets);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore Stats: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ProcessCounters(List<TicketViewModel> tickets)
        {
            CountOpen = tickets.Count(t => t.StatoId == 1);
            CountInProgress = tickets.Count(t => t.StatoId == 2);
            CountClosed = tickets.Count(t => t.StatoId == 3);
        }

        private void ProcessReportData(List<TicketViewModel> allTickets)
        {
            if (DateStart.Date == null || DateEnd.Date == null) return;

            DateTime start = DateStart.Date.Value.DateTime.Date;
            DateTime end = DateEnd.Date.Value.DateTime.Date.AddDays(1).AddSeconds(-1);

            // --- FILTRO ---
            // Include SOLO i ticket che sono TERMINATI (StatoId == 3)
            // e che sono stati creati nel range di date selezionato
            var filtered = allTickets
                .Where(t => {
                    var localDate = t.DataCreazione.ToLocalTime();
                    return localDate >= start &&
                           localDate <= end &&
                           t.StatoId == 3; // <--- FILTRO FONDAMENTALE
                })
                .ToList();

            // Grafici a Torta (Report)
            ReportUrgencySeries = CreatePieSeries(filtered.GroupBy(t => t.UrgenzaNome));
            ReportSedeSeries = CreatePieSeries(filtered.GroupBy(t => t.SedeNome));
            ReportTypeSeries = CreatePieSeries(filtered.GroupBy(t => t.TipologiaNome));

            // Grafico a Barre (Utenti)
            // Conta chi ha chiuso i ticket
            var userGroup = filtered
                .Where(t => !string.IsNullOrEmpty(t.AssegnatoaNome) && t.AssegnatoaNome != "Non assegnato")
                .GroupBy(t => t.AssegnatoaNome)
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
                new Axis
                {
                    Labels = userGroup.Select(g => g.Key).ToList(),
                    LabelsRotation = 15,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray)
                }
            };

            // KPI
            // Calcola la % di ticket assegnati vs totali CHIUSI
            int total = filtered.Count;
            int assigned = filtered.Count(t => t.AssegnatoaId != null && t.AssegnatoaId != 0);
            AssignmentRate = total > 0 ? $"{(double)assigned / total:P0}" : "0%";
            AvgCloseTime = "N/D";
        }

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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}