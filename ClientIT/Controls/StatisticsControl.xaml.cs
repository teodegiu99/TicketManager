using ClientIT.Models;
using LiveChartsCore;
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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI;

namespace ClientIT.Controls
{
    public sealed partial class StatisticsControl : UserControl, INotifyPropertyChanged
    {
        private HttpClient _apiClient;
        private string _apiBaseUrl = "http://localhost:5210";

        // Properties
        private int _countOpen;
        private int _countInProgress;
        private int _countClosed;
        private bool _isLoading;

        private ISeries[] _urgencySeries = Array.Empty<ISeries>();
        private ISeries[] _colorSeries = Array.Empty<ISeries>();
        private ISeries[] _typeSeries = Array.Empty<ISeries>(); // NUOVO

        public int CountOpen { get => _countOpen; set { _countOpen = value; OnPropertyChanged(); } }
        public int CountInProgress { get => _countInProgress; set { _countInProgress = value; OnPropertyChanged(); } }
        public int CountClosed { get => _countClosed; set { _countClosed = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        public ISeries[] UrgencySeries { get => _urgencySeries; set { _urgencySeries = value; OnPropertyChanged(); } }
        public ISeries[] ColorSeries { get => _colorSeries; set { _colorSeries = value; OnPropertyChanged(); } }
        public ISeries[] TypeSeries { get => _typeSeries; set { _typeSeries = value; OnPropertyChanged(); } } // NUOVO

        public StatisticsControl()
        {
            this.InitializeComponent();
            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (s, c, ch, e) => true };
            _apiClient = new HttpClient(handler);

            this.Loaded += StatisticsControl_Loaded;
        }

        private async void StatisticsControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadStats();
        }

        public async Task LoadStats()
        {
            IsLoading = true;
            try
            {
                // Richiediamo TUTTI i ticket per i contatori totali
                string url = $"{_apiBaseUrl}/api/tickets/all?includeAll=true";
                var allTickets = await _apiClient.GetFromJsonAsync<List<TicketViewModel>>(url);

                if (allTickets != null)
                {
                    // 1. Aggiorna i contatori (usando tutti i ticket)
                    ProcessCounters(allTickets);

                    // 2. Filtra SOLO i ticket NON terminati per i grafici
                    var activeTickets = allTickets.Where(t => t.StatoId != 3).ToList();

                    // 3. Genera i grafici
                    ProcessUrgencyChart(activeTickets);
                    ProcessTypeChart(activeTickets); // NUOVO
                    ProcessColorChart(activeTickets);
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

        private void ProcessCounters(List<TicketViewModel> tickets)
        {
            CountOpen = tickets.Count(t => t.StatoId == 1);
            CountInProgress = tickets.Count(t => t.StatoId == 2);
            CountClosed = tickets.Count(t => t.StatoId == 3);
        }

        private void ProcessUrgencyChart(List<TicketViewModel> activeTickets)
        {
            // Raggruppa i ticket ATTIVI per urgenza
            var grouped = activeTickets
                .GroupBy(t => t.UrgenzaNome)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToList();

            var seriesList = new List<ISeries>();
            foreach (var item in grouped)
            {
                seriesList.Add(new PieSeries<int>
                {
                    Values = new[] { item.Count },
                    Name = item.Name,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}",
                    // Imposta tooltip personalizzato
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Coordinate.PrimaryValue} ticket"
                });
            }
            UrgencySeries = seriesList.ToArray();
        }

        private void ProcessTypeChart(List<TicketViewModel> activeTickets)
        {
            // Raggruppa i ticket ATTIVI per tipologia
            var grouped = activeTickets
                .GroupBy(t => t.TipologiaNome)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToList();

            var seriesList = new List<ISeries>();
            foreach (var item in grouped)
            {
                seriesList.Add(new PieSeries<int>
                {
                    Values = new[] { item.Count },
                    Name = item.Name,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}",
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Coordinate.PrimaryValue} ticket"
                });
            }
            TypeSeries = seriesList.ToArray();
        }

        private void ProcessColorChart(List<TicketViewModel> activeTickets)
        {
            int green = 0;
            int yellow = 0;
            int red = 0;

            foreach (var t in activeTickets)
            {
                // Sfrutta la logica BusinessTimeCalculator presente nel ViewModel
                var brush = t.StatusBorderBrush;
                var color = brush.Color;

                if (color == Colors.LimeGreen) green++;
                else if (color == Colors.Orange) yellow++;
                else if (color == Colors.Red) red++;
            }

            // Mostra solo se > 0 per evitare fette vuote con etichette strane
            var seriesList = new List<ISeries>();

            if (green > 0)
                seriesList.Add(new PieSeries<int> { Values = new[] { green }, Name = "Nei tempi", Fill = new SolidColorPaint(SKColors.LimeGreen) });

            if (yellow > 0)
                seriesList.Add(new PieSeries<int> { Values = new[] { yellow }, Name = "In scadenza", Fill = new SolidColorPaint(SKColors.Orange) });

            if (red > 0)
                seriesList.Add(new PieSeries<int> { Values = new[] { red }, Name = "Scaduti", Fill = new SolidColorPaint(SKColors.Red) });

            ColorSeries = seriesList.ToArray();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}