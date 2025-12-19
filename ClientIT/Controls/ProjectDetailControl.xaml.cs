using ClientIT.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // Necessario per INotifyPropertyChanged
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices; // Necessario per CallerMemberName
using System.Threading.Tasks;

namespace ClientIT.Controls
{
    public sealed partial class ProjectDetailControl : UserControl, INotifyPropertyChanged
    {
        // =========================
        // PROPRIETÀ BINDABLE
        // =========================
        private ProjectViewModel _project;
        public ProjectViewModel Project
        {
            get => _project;
            set { _project = value; OnPropertyChanged(); }
        }

        // ObservableCollection non ha bisogno di set notification se l'istanza non cambia
        public ObservableCollection<CommentoViewModel> Comments { get; } = new();

        public event EventHandler BackRequested;
        public event PropertyChangedEventHandler PropertyChanged;

        private HttpClient _apiClient;
        private string _baseUrl = "http://localhost:5210";
        private ItUtente _currentUser;

        public ProjectDetailControl()
        {
            this.InitializeComponent();
            // Configura HttpClient (puoi anche iniettarlo o prenderlo da App.xaml.cs)
            _apiClient = new HttpClient(new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });
        }

        // =========================
        // METODI
        // =========================
        public void Load(ProjectViewModel project, ItUtente currentUser)
        {
            _currentUser = currentUser;

            // Impostando la proprietà Project, scatta OnPropertyChanged e la UI si aggiorna da sola
            Project = project;

            // Carica i commenti in background
            _ = LoadComments();
        }

        private async Task LoadComments()
        {
            try
            {
                Comments.Clear(); // Pulisce la lista visiva
                var list = await _apiClient.GetFromJsonAsync<List<CommentoViewModel>>($"{_baseUrl}/api/progetti/{Project.Id}/commenti");

                if (list != null)
                {
                    foreach (var c in list)
                    {
                        bool isMe = c.Username == _currentUser?.UsernameAd || c.Username == _currentUser?.Nome;

                        c.Allineamento = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left;

                        // Nota: Accesso alle risorse sicuro
                        if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var accentColor) && isMe)
                        {
                            c.Sfondo = (SolidColorBrush)accentColor;
                        }
                        else
                        {
                            c.Sfondo = new SolidColorBrush(Colors.WhiteSmoke); // Fallback o colore per gli altri
                            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var cardColor))
                                c.Sfondo = (SolidColorBrush)cardColor;
                        }

                        Comments.Add(c);
                    }

                    // Scrolla in fondo
                    if (Comments.Any() && CommentsList != null)
                        CommentsList.ScrollIntoView(Comments.Last());
                }
            }
            catch (Exception ex)
            {
                // Gestione errore silenziosa o dialog
                System.Diagnostics.Debug.WriteLine($"Errore caricamento commenti: {ex.Message}");
            }
        }

        private async void SendComment_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCommento.Text) || Project == null) return;

            var dto = new
            {
                Testo = TxtCommento.Text,
                UtenteId = _currentUser?.Id,
                Username = _currentUser?.Nome ?? "Utente"
            };

            try
            {
                var res = await _apiClient.PostAsJsonAsync($"{_baseUrl}/api/progetti/{Project.Id}/commenti", dto);
                if (res.IsSuccessStatusCode)
                {
                    TxtCommento.Text = "";
                    await LoadComments();
                }
            }
            catch { }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        // =========================
        // HELPER NOTIFY
        // =========================
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}