using ClientIT.Models;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
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
using System.Threading.Tasks;

namespace ClientIT.Controls
{
    // =========================
    // MODELLO GANTT
    // =========================
    public sealed class GanttPoint
    {
        public double Start { get; }
        public double End { get; }

        public GanttPoint(double start, double end)
        {
            Start = start;
            End = end;
        }
    }

    public sealed partial class NewProjectControl : UserControl, INotifyPropertyChanged
    {
        // =========================
        // DATI
        // =========================
        public ObservableCollection<PhaseViewModel> Phases { get; } = new();

        private List<ItUtente> _allUsers = new();
        private List<Stato> _allStati = new();
        private readonly HttpClient _apiClient;

        // =========================
        // PROPRIETÀ GANTT (WINUI)
        // =========================
        private ISeries[] _ganttSeries = Array.Empty<ISeries>();
        private Axis[] _ganttXAxes = Array.Empty<Axis>();
        private Axis[] _ganttYAxes = Array.Empty<Axis>();

        private double _chartWidth = 1200;
        private double _chartHeight = 300;
        private bool _hasPhases;

        public ISeries[] GanttSeries
        {
            get => _ganttSeries;
            set { _ganttSeries = value; OnPropertyChanged(); }
        }

        public Axis[] GanttXAxes
        {
            get => _ganttXAxes;
            set { _ganttXAxes = value; OnPropertyChanged(); }
        }

        public Axis[] GanttYAxes
        {
            get => _ganttYAxes;
            set { _ganttYAxes = value; OnPropertyChanged(); }
        }

        public double ChartWidth
        {
            get => _chartWidth;
            set { _chartWidth = value; OnPropertyChanged(); }
        }

        public double ChartHeight
        {
            get => _chartHeight;
            set { _chartHeight = value; OnPropertyChanged(); }
        }

        public bool HasPhases
        {
            get => _hasPhases;
            set { _hasPhases = value; OnPropertyChanged(); }
        }

        // =========================
        // NOTIFY
        // =========================
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // =========================
        // COSTRUTTORE
        // =========================
        public NewProjectControl()
        {
            InitializeComponent();

            // 🔴 FONDAMENTALE per i binding
            DataContext = this;

            PhasesListView.ItemsSource = Phases;

            _apiClient = new HttpClient(new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });

            Phases.CollectionChanged += Phases_CollectionChanged;
        }

        // =========================
        // DATI ESTERNI
        // =========================
        public void SetupReferenceData(List<ItUtente> users, List<Stato> stati)
        {
            _allUsers = users ?? new();
            _allStati = stati ?? new();
        }

        // =========================
        // EVENTI PHASES
        // =========================
        private void Phases_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (PhaseViewModel p in e.NewItems)
                    p.PropertyChanged += Phase_PropertyChanged;

            if (e.OldItems != null)
                foreach (PhaseViewModel p in e.OldItems)
                    p.PropertyChanged -= Phase_PropertyChanged;

            UpdateGanttChart();
        }

        private void Phase_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateGanttChart();
        }

        // =========================
        // CORE GANTT
        // =========================
        private void UpdateGanttChart()
        {
            if (!Phases.Any())
            {
                GanttSeries = Array.Empty<ISeries>();
                HasPhases = false;
                return;
            }

            HasPhases = true;

            // --- fasi con date valide
            var valid = Phases
                .Where(p => p.DataInizio.HasValue && p.DataPrevFine.HasValue)
                .ToList();

            // --- limiti temporali REALI
            var min = valid.Any()
                ? valid.Min(p => p.DataInizio!.Value.UtcDateTime)
                : DateTime.UtcNow;

            var max = valid.Any()
                ? valid.Max(p => p.DataPrevFine!.Value.UtcDateTime)
                : DateTime.UtcNow.AddDays(1);

            var viewMin = min.AddDays(-2);
            var viewMax = max.AddDays(5);
            var totalDays = Math.Max(1, (viewMax - viewMin).TotalDays);

            // --- dimensioni → scroll orizzontale
            ChartWidth = Math.Max(1200, totalDays * 60);
            ChartHeight = Math.Max(300, Phases.Count * 60);

            // =========================
            // ASSE X (GIORNI)
            // =========================
            GanttXAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = totalDays,
                    UnitWidth = 1,   // 1 = 1 giorno
                    MinStep = 1,
                    TextSize = 12,
                    Labeler = v =>
                    {
                        if (v < 0 || v > totalDays) return "";
                        return viewMin.AddDays(v).ToString("dd/MM");
                    },
                    SeparatorsPaint = new SolidColorPaint(new SKColor(210,210,210))
                }
            };

            // =========================
            // ASSE Y
            // =========================
            GanttYAxes = new[]
            {
                new Axis
                {
                    Labels = Phases.Select(p =>
                        string.IsNullOrWhiteSpace(p.Titolo) ? "(senza nome)" : p.Titolo).ToList(),
                    IsInverted = true,
                    UnitWidth = 1,
                    MinStep = 1,
                    TextSize = 14
                }
            };

            // =========================
            // DATI GANTT
            // =========================
            var values = new List<GanttPoint>();

            foreach (var p in Phases)
            {
                if (p.DataInizio.HasValue && p.DataPrevFine.HasValue)
                {
                    var start = (p.DataInizio.Value.UtcDateTime - viewMin).TotalDays;
                    var end = (p.DataPrevFine.Value.UtcDateTime - viewMin).TotalDays;
                    if (end <= start) end = start + 1;
                    values.Add(new GanttPoint(start, end));
                }
                else
                {
                    // riga senza barra
                    values.Add(new GanttPoint(double.NaN, double.NaN));
                }
            }

            // =========================
            // SERIE (BARRE + OGGI)
            // =========================
            var series = new List<ISeries>
            {
                // BARRE GANTT
                new RowSeries<GanttPoint>
                {
                    Values = values,
                    MaxBarWidth = 40,
                    Rx = 4,
                    Ry = 4,
                    Fill = new SolidColorPaint(SKColors.Orange),

                    Mapping = (point, index) =>
                    {
                        if (double.IsNaN(point.Start) || double.IsNaN(point.End))
                            return new Coordinate(double.NaN, index);

                        // X = fine, Secondary = inizio
                        return new Coordinate(point.End, index, point.Start);
                    },

                    DataLabelsPosition = DataLabelsPosition.Middle,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsFormatter = p =>
                    {
                        if (p.Model is not GanttPoint g ||
                            double.IsNaN(g.Start) || double.IsNaN(g.End))
                            return "";

                        return $"{viewMin.AddDays(g.Start):dd/MM} - {viewMin.AddDays(g.End):dd/MM}";
                    }
                }
            };

            // =========================
            // LINEA VERTICALE "OGGI"
            // =========================
            var todayX = (DateTime.UtcNow - viewMin).TotalDays;

            if (todayX >= 0 && todayX <= totalDays)
            {
                series.Add(new LineSeries<double>
                {
                    Values = new[] { todayX, todayX },
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                    LineSmoothness = 0
                });
            }

            GanttSeries = series.ToArray();
        }

        // =========================
        // UI HANDLER
        // =========================
        private void BtnAddPhase_Click(object sender, RoutedEventArgs e)
        {
            Phases.Add(new PhaseViewModel
            {
                Titolo = "Nuova fase"
                // date vuote → nessuna barra
            });
        }

        private void BtnRemovePhase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is PhaseViewModel p)
                Phases.Remove(p);
        }

        private async void PhasesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PhaseViewModel phase)
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
                    phase.Titolo = updated.Titolo;
                    phase.Descrizione = updated.Descrizione;
                    phase.DataInizio = updated.DataInizio;
                    phase.DataPrevFine = updated.DataPrevFine;
                    phase.Stato = updated.Stato;
                    phase.AssegnatoA = updated.AssegnatoA;

                    UpdateGanttChart();
                }
            }
        }

        private async void BtnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTitolo.Text))
            {
                await ShowDialog("Errore", "Il titolo del progetto è obbligatorio.");
                return;
            }

            BtnSaveProject.IsEnabled = false;
            SavingRing.Visibility = Visibility.Visible;
            SavingRing.IsActive = true;

            try
            {
                var dto = new
                {
                    Titolo = TxtTitolo.Text,
                    Descrizione = TxtDescrizione.Text,
                    Fasi = Phases.Select((p, i) => new
                    {
                        Titolo = p.Titolo,
                        Descrizione = p.Descrizione,
                        DataInizio = p.DataInizio?.DateTime,
                        DataPrevFine = p.DataPrevFine?.DateTime,
                        StatoId = p.Stato?.Id ?? 1,
                        Ordine = i,
                        AssegnatoAId = (p.AssegnatoA != null && p.AssegnatoA.Id > 0)
                            ? (int?)p.AssegnatoA.Id
                            : null
                    }).ToList()
                };

                var res = await _apiClient.PostAsJsonAsync("http://localhost:5210/api/progetti", dto);

                if (res.IsSuccessStatusCode)
                {
                    await ShowDialog("Successo", "Progetto creato correttamente!");
                    Phases.Clear();
                    TxtTitolo.Text = "";
                    TxtDescrizione.Text = "";
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

            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
