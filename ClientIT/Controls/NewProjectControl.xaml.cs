using ClientIT.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ClientIT.Controls
{
    // ⭐ CLASSE GANTTPOINT PERSONALIZZATA
    public class GanttPoint
    {
        public double Start { get; set; }
        public double End { get; set; }

        public GanttPoint(double start, double end)
        {
            Start = start;
            End = end;
        }
    }

    public sealed partial class NewProjectControl : UserControl, System.ComponentModel.INotifyPropertyChanged
    {
        public ObservableCollection<PhaseViewModel> Phases { get; } = new();
        private List<ItUtente> _allUsers = new();
        private List<Stato> _allStati = new();
        private HttpClient _apiClient;

        // Proprietà per il Gantt - ⭐ USA I TIPI CORRETTI PER IL BINDING
        private ISeries[] _ganttSeries;
        private IEnumerable<LiveChartsCore.Kernel.Sketches.ICartesianAxis> _ganttXAxes;
        private IEnumerable<LiveChartsCore.Kernel.Sketches.ICartesianAxis> _ganttYAxes;
        private bool _hasPhases;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        public ISeries[] GanttSeries { get => _ganttSeries; set { _ganttSeries = value; OnPropertyChanged(); } }
        public IEnumerable<LiveChartsCore.Kernel.Sketches.ICartesianAxis> GanttXAxes { get => _ganttXAxes; set { _ganttXAxes = value; OnPropertyChanged(); } }
        public IEnumerable<LiveChartsCore.Kernel.Sketches.ICartesianAxis> GanttYAxes { get => _ganttYAxes; set { _ganttYAxes = value; OnPropertyChanged(); } }
        public bool HasPhases { get => _hasPhases; set { _hasPhases = value; OnPropertyChanged(); } }

        public NewProjectControl()
        {
            this.InitializeComponent();
            PhasesListView.ItemsSource = Phases;

            // Inizializza Client HTTP
            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (s, c, ch, e) => true
            };
            _apiClient = new HttpClient(handler);

            // Reagisci ai cambiamenti della lista per aggiornare il Gantt
            Phases.CollectionChanged += (s, e) => UpdateGanttChart();
        }

        public void SetupReferenceData(List<ItUtente> users, List<Stato> stati)
        {
            _allUsers = users;
            _allStati = stati;
        }

        // --- LOGICA GANTT ---
        private void UpdateGanttChart()
        {
            HasPhases = Phases.Any();
            if (!HasPhases)
            {
                GanttSeries = Array.Empty<ISeries>();
                return;
            }

            // Filtra solo fasi con date valide
            var validPhases = Phases.Where(p => p.DataInizio.HasValue && p.DataPrevFine.HasValue).ToList();
            if (!validPhases.Any())
            {
                GanttSeries = Array.Empty<ISeries>();
                return;
            }

            // Configura Asse X (Tempo)
            GanttXAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value => new DateTime((long)value).ToString("dd/MM"),
                    UnitWidth = TimeSpan.FromDays(1).Ticks,
                    MinStep = TimeSpan.FromDays(1).Ticks
                }
            };

            // Configura Asse Y (Nomi Fasi)
            GanttYAxes = new Axis[]
            {
                new Axis
                {
                    Labels = validPhases.Select(p => p.Titolo).ToList(),
                    LabelsRotation = 0,
                }
            };

            // Crea la serie per il Gantt usando RowSeries
            var values = new List<GanttPoint>();
            for (int i = 0; i < validPhases.Count; i++)
            {
                var p = validPhases[i];
                values.Add(new GanttPoint(p.DataInizio.Value.Ticks, p.DataPrevFine.Value.Ticks));
            }

            GanttSeries = new ISeries[]
            {
                new RowSeries<GanttPoint>
                {
                    Values = values,
                    Mapping = (point, index) => new LiveChartsCore.Kernel.Coordinate(index, point.Start, point.End - point.Start),
                    DataLabelsFormatter = point =>
                    {
                        var gantt = point.Model as GanttPoint;
                        if (gantt == null) return "";
                        return $"{new DateTime((long)gantt.Start):dd/MM} - {new DateTime((long)gantt.End):dd/MM}";
                    },
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Middle,
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue)
                }
            };
        }

        // --- GESTORI EVENTI UI ---
        private void BtnAddPhase_Click(object sender, RoutedEventArgs e)
        {
            var newPhase = new PhaseViewModel
            {
                Titolo = "Nuova Fase",
                Descrizione = "",
                DataInizio = DateTimeOffset.Now,
                DataPrevFine = DateTimeOffset.Now.AddDays(7)
            };
            Phases.Add(newPhase);
        }

        private void PhasesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PhaseViewModel phase)
            {
                // Logica per modificare/eliminare la fase
                // Puoi aprire un dialog o navigare a un'altra view
            }
        }

        // --- LOGICA SALVATAGGIO ---
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
                // Prepara il DTO
                var projectDto = new
                {
                    Titolo = TxtTitolo.Text,
                    Descrizione = TxtDescrizione.Text,
                    Fasi = Phases.Select((p, index) => new
                    {
                        Titolo = p.Titolo,
                        Descrizione = p.Descrizione,
                        DataInizio = p.DataInizio?.DateTime,
                        DataPrevFine = p.DataPrevFine?.DateTime,
                        StatoId = p.Stato?.Id ?? 1,
                        Ordine = index,
                        AssegnatoAId = (p.AssegnatoA != null && p.AssegnatoA.Id > 0) ? (int?)p.AssegnatoA.Id : null,
                        AssegnatoAEsterno = (p.AssegnatoA != null && p.AssegnatoA.Id == -999) ? "Utente Esterno" : null
                    }).ToList()
                };

                // Chiamata API (⚠️ Modifica l'URL se necessario)
                string url = "http://localhost:5210/api/progetti";
                var response = await _apiClient.PostAsJsonAsync(url, projectDto);

                if (response.IsSuccessStatusCode)
                {
                    await ShowDialog("Successo", "Progetto creato correttamente!");
                    // Resetta il form
                    TxtTitolo.Text = "";
                    TxtDescrizione.Text = "";
                    Phases.Clear();
                }
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    await ShowDialog("Errore API", $"Impossibile salvare: {err}");
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
            if (this.XamlRoot == null) return;
            var d = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await d.ShowAsync();
        }
    }
}