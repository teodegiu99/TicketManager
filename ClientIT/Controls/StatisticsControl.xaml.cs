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
using System.Reflection; // Necessario per la Reflection dei colori
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
        private ISeries[] _typeSeries = Array.Empty<ISeries>();

        // Proprietà per il colore del testo legenda
        public SolidColorPaint LegendTextPaint { get; set; } = new SolidColorPaint(SKColors.White);

        public int CountOpen { get => _countOpen; set { _countOpen = value; OnPropertyChanged(); } }
        public int CountInProgress { get => _countInProgress; set { _countInProgress = value; OnPropertyChanged(); } }
        public int CountClosed { get => _countClosed; set { _countClosed = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        public ISeries[] UrgencySeries { get => _urgencySeries; set { _urgencySeries = value; OnPropertyChanged(); } }
        public ISeries[] ColorSeries { get => _colorSeries; set { _colorSeries = value; OnPropertyChanged(); } }
        public ISeries[] TypeSeries { get => _typeSeries; set { _typeSeries = value; OnPropertyChanged(); } }

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

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
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
                    ProcessTypeChart(activeTickets);
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
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Coordinate.PrimaryValue} ticket"
                });
            }
            UrgencySeries = seriesList.ToArray();
        }

        private void ProcessTypeChart(List<TicketViewModel> activeTickets)
        {
            // MODIFICA QUI: Raggruppa per Nome E Colore
            var grouped = activeTickets
                .GroupBy(t => new { t.TipologiaNome, t.TipologiaColore })
                .Select(g => new {
                    Name = g.Key.TipologiaNome,
                    ColorString = g.Key.TipologiaColore,
                    Count = g.Count()
                })
                .ToList();

            var seriesList = new List<ISeries>();
            foreach (var item in grouped)
            {
                // Converte la stringa (Hex o Nome) in un colore SkiaSharp
                SKColor sliceColor = GetSkColorFromString(item.ColorString);

                seriesList.Add(new PieSeries<int>
                {
                    Values = new[] { item.Count },
                    Name = item.Name,
                    Fill = new SolidColorPaint(sliceColor), // <--- Applica il colore specifico

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
                var brush = t.StatusBorderBrush;
                var c = brush.Color;

                if (AreColorsEqual(c, Colors.LimeGreen)) green++;
                else if (AreColorsEqual(c, Colors.Orange)) yellow++;
                else if (AreColorsEqual(c, Colors.Red)) red++;
            }

            var seriesList = new List<ISeries>();

            if (green > 0)
                seriesList.Add(new PieSeries<int> { Values = new[] { green }, Name = "Nei tempi", Fill = new SolidColorPaint(SKColors.LimeGreen) });

            if (yellow > 0)
                seriesList.Add(new PieSeries<int> { Values = new[] { yellow }, Name = "In scadenza", Fill = new SolidColorPaint(SKColors.Orange) });

            if (red > 0)
                seriesList.Add(new PieSeries<int> { Values = new[] { red }, Name = "Scaduti", Fill = new SolidColorPaint(SKColors.Red) });

            ColorSeries = seriesList.ToArray();
        }

        // Helper per confrontare i colori Windows.UI.Color
        private bool AreColorsEqual(Color c1, Color c2)
        {
            return c1.A == c2.A && c1.R == c2.R && c1.G == c2.G && c1.B == c2.B;
        }

        // Helper per convertire stringa (Hex o Nome) in SKColor (per i grafici)
        private SKColor GetSkColorFromString(string? colorStr)
        {
            if (string.IsNullOrEmpty(colorStr)) return SKColors.Gray;

            try
            {
                // Se è Hex
                if (colorStr.StartsWith("#"))
                {
                    return SKColor.Parse(colorStr);
                }

                // Se è un nome (es. "Red"), cerca in SKColors tramite Reflection
                var field = typeof(SKColors).GetField(colorStr, BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    return (SKColor)field.GetValue(null)!;
                }
            }
            catch { }

            return SKColors.Gray; // Fallback
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}