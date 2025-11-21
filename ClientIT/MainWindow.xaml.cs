using ClientIT.Controls; // Necessario per gli eventi TicketStateChangedEventArgs
using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Fondamentale per il Binding dinamico
using System.Net.Http;
using System.Net.Http.Json; // Per PutAsJsonAsync
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace ClientIT
{
    public sealed partial class MainWindow : Window
    {
        private HttpClient _apiClient;

        // ⚠️ Controlla che l'URL sia corretto
        private string _apiBaseUrl = "http://localhost:5210";

        // --- LISTE PUBBLICHE PER I COMBOBOX ---
        // Usiamo ObservableCollection così la UI si accorge quando aggiungiamo elementi.
        public ObservableCollection<Stato> AllStati { get; } = new();
        public ObservableCollection<ItUtente> AllItUsers { get; } = new();

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Gestione Ticket (IT)";

            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _apiClient = new HttpClient(handler);

            // Carichiamo i dati quando la finestra viene attivata per la prima volta
            this.Activated += MainWindow_Activated;
        }

        private bool _isFirstActivation = true;

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (_isFirstActivation)
            {
                _isFirstActivation = false;
                _ = LoadDataAsync();
            }
        }

        // --- CARICAMENTO DATI ---

        private async Task LoadDataAsync()
        {
            // Gestione UI di caricamento
            if (LoadingProgressRing != null)
            {
                LoadingProgressRing.IsActive = true;
                LoadingProgressRing.Visibility = Visibility.Visible;
            }
            if (RootGrid != null) RootGrid.IsHitTestVisible = false;

            try
            {
                // 1. Benvenuto
                if (WelcomeMessage != null && App.CurrentUser != null)
                {
                    WelcomeMessage.Text = $"Bentornato, {App.CurrentUser.UsernameAd} ({App.CurrentUser.Permesso})";
                }

                // 2. Carica le liste di supporto (Stati e Utenti) PRIMA dei ticket
                // Questo assicura che i ComboBox abbiano i dati pronti quando i ticket arrivano
                await LoadStatiAsync();
                await LoadItUsersAsync();

                // 3. Carica i ticket (inizialmente tutti)
                await LoadTicketsAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Errore nel caricamento dati: {ex.Message}");
            }
            finally
            {
                if (LoadingProgressRing != null)
                {
                    LoadingProgressRing.IsActive = false;
                    LoadingProgressRing.Visibility = Visibility.Collapsed;
                }
                if (RootGrid != null) RootGrid.IsHitTestVisible = true;
            }
        }

        private async Task LoadStatiAsync()
        {
            var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/tickets/stati");
            response.EnsureSuccessStatusCode();

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var stati = JsonSerializer.Deserialize<List<Stato>>(jsonResponse, options);

            AllStati.Clear();
            if (stati != null)
            {
                foreach (var s in stati) AllStati.Add(s);
            }
        }

        private async Task LoadItUsersAsync()
        {
            var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/auth/users");
            response.EnsureSuccessStatusCode();

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var utenti = JsonSerializer.Deserialize<List<ItUtente>>(jsonResponse, options);

            // 1. Popola la lista visiva a sinistra (solo utenti reali)
            if (UserListView != null)
            {
                UserListView.ItemsSource = utenti;
            }

            // 2. Popola la lista per i ComboBox (aggiungendo l'opzione "Non assegnato")
            AllItUsers.Clear();
            // Usiamo la proprietà statica definita nel Modello per coerenza
            var nonAssegnato = ItUtente.NonAssegnato ?? new ItUtente { Id = 0, UsernameAd = "Non assegnato" };
            AllItUsers.Add(nonAssegnato);

            if (utenti != null)
            {
                foreach (var u in utenti) AllItUsers.Add(u);
            }
        }

        private async Task LoadTicketsAsync(int? assegnatoa_id = null)
        {
            // Mostra caricamento solo se non stiamo già caricando tutto (micro-ottimizzazione visiva)
            if (LoadingProgressRing != null && !LoadingProgressRing.IsActive)
            {
                LoadingProgressRing.IsActive = true;
                LoadingProgressRing.Visibility = Visibility.Visible;
            }

            // Svuota la lista per feedback visivo immediato
            if (TicketListView != null) TicketListView.ItemsSource = null;

            try
            {
                string url = $"{_apiBaseUrl}/api/tickets/all";
                if (assegnatoa_id.HasValue)
                {
                    url += $"?assegnatoa_id={assegnatoa_id.Value}";
                }

                var response = await _apiClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var tickets = JsonSerializer.Deserialize<List<TicketViewModel>>(jsonResponse, options);

                if (TicketListView != null) TicketListView.ItemsSource = tickets;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Impossibile caricare i ticket: {ex.Message}");
            }
            finally
            {
                if (LoadingProgressRing != null)
                {
                    LoadingProgressRing.IsActive = false;
                    LoadingProgressRing.Visibility = Visibility.Collapsed;
                }
            }
        }

        // --- GESTORI EVENTI UI (FILTRI) ---

        private async void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserListView != null) UserListView.SelectedIndex = -1;
            await LoadTicketsAsync(null);
        }

        private async void UserListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UserListView?.SelectedItem is ItUtente selectedUser)
            {
                // Filtra la lista dei ticket per l'ID di questo utente
                await LoadTicketsAsync(selectedUser.Id);
            }
        }

        // --- GESTORI EVENTI TICKET (AUTO-SALVATAGGIO) ---

        // Questi metodi vengono chiamati dal TicketItemControl tramite evento.
        // Riceviamo i dati puliti: ID del ticket e il nuovo valore.

        public async void OnTicketStateChanged(object sender, TicketStateChangedEventArgs e)
        {
            await UpdateTicketAsync(e.Nticket, e.StatoId, null);
        }

        public async void OnTicketAssigneeChanged(object sender, TicketAssigneeChangedEventArgs e)
        {
            await UpdateTicketAsync(e.Nticket, null, e.AssegnatoaId);
        }

        // --- LOGICA API PUT ---

        private async Task UpdateTicketAsync(int nticket, int? statoId, int? assegnatoaId)
        {
            // Blocca l'interfaccia per evitare modifiche concorrenti rapide
            if (RootGrid != null) RootGrid.IsHitTestVisible = false;
            if (LoadingProgressRing != null) { LoadingProgressRing.IsActive = true; LoadingProgressRing.Visibility = Visibility.Visible; }

            try
            {
                string url = $"{_apiBaseUrl}/api/tickets/{nticket}/update";

                var request = new
                {
                    StatoId = statoId,
                    AssegnatoaId = assegnatoaId
                };

                var response = await _apiClient.PutAsJsonAsync(url, request);
                response.EnsureSuccessStatusCode();

                // Successo. Non serve ricaricare la lista perché il ComboBox 
                // nel TicketItemControl si è già aggiornato visivamente.
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Errore salvataggio: {ex.Message}");

                // Se fallisce, ricarichiamo la lista per ripristinare i dati corretti dal DB
                // (così l'utente vede che la modifica non è andata a buon fine)
                var currentFilterId = (UserListView?.SelectedItem as ItUtente)?.Id;
                await LoadTicketsAsync(currentFilterId);
            }
            finally
            {
                if (RootGrid != null) RootGrid.IsHitTestVisible = true;
                if (LoadingProgressRing != null) { LoadingProgressRing.IsActive = false; LoadingProgressRing.Visibility = Visibility.Collapsed; }
            }
        }

        private async Task ShowErrorDialog(string content)
        {
            if (RootGrid?.XamlRoot == null) return;
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Errore",
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = RootGrid.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }
}