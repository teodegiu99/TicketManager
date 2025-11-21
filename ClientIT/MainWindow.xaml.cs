using ClientIT.Controls;
using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClientIT
{
    public sealed partial class MainWindow : Window
    {
        private HttpClient _apiClient;
        private string _apiBaseUrl = "http://localhost:5210"; // Controlla la porta!

        // --- LISTE PUBBLICHE PER I COMBOBOX ---
        public ObservableCollection<Stato> AllStati { get; } = new();
        public ObservableCollection<ItUtente> AllItUsers { get; } = new();

        // NUOVE LISTE
        public ObservableCollection<Tipologia> AllTipologie { get; } = new();
        public ObservableCollection<Urgenza> AllUrgenze { get; } = new();

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
            if (LoadingProgressRing != null)
            {
                LoadingProgressRing.IsActive = true;
                LoadingProgressRing.Visibility = Visibility.Visible;
            }
            if (RootGrid != null) RootGrid.IsHitTestVisible = false;

            try
            {
                if (WelcomeMessage != null && App.CurrentUser != null)
                {
                    WelcomeMessage.Text = $"Bentornato, {App.CurrentUser.UsernameAd} ({App.CurrentUser.Permesso})";
                }

                // Carica tutte le liste di riferimento
                await LoadReferenceDataAsync();

                // Carica i ticket
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

        private async Task LoadReferenceDataAsync()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // 1. Stati
            var stati = await _apiClient.GetFromJsonAsync<List<Stato>>($"{_apiBaseUrl}/api/tickets/stati", options);
            AllStati.Clear();
            if (stati != null) foreach (var s in stati) AllStati.Add(s);

            // 2. Utenti IT
            var utenti = await _apiClient.GetFromJsonAsync<List<ItUtente>>($"{_apiBaseUrl}/api/auth/users", options);
            AllItUsers.Clear();
            var nonAssegnato = ItUtente.NonAssegnato ?? new ItUtente { Id = 0, UsernameAd = "Non assegnato" };
            AllItUsers.Add(nonAssegnato);

            if (utenti != null)
            {
                if (UserListView != null) UserListView.ItemsSource = utenti;
                foreach (var u in utenti) AllItUsers.Add(u);
            }

            // 3. Tipologie (NUOVO)
            var tipologie = await _apiClient.GetFromJsonAsync<List<Tipologia>>($"{_apiBaseUrl}/api/tickets/tipologie", options);
            AllTipologie.Clear();
            if (tipologie != null) foreach (var t in tipologie) AllTipologie.Add(t);

            // 4. Urgenze (NUOVO)
            var urgenze = await _apiClient.GetFromJsonAsync<List<Urgenza>>($"{_apiBaseUrl}/api/tickets/urgenze", options);
            AllUrgenze.Clear();
            if (urgenze != null) foreach (var u in urgenze) AllUrgenze.Add(u);
        }

        private async Task LoadTicketsAsync(int? assegnatoa_id = null)
        {
            if (TicketListView != null) TicketListView.ItemsSource = null;

            try
            {
                string url = $"{_apiBaseUrl}/api/tickets/all";
                if (assegnatoa_id.HasValue) url += $"?assegnatoa_id={assegnatoa_id.Value}";

                var tickets = await _apiClient.GetFromJsonAsync<List<TicketViewModel>>(url);
                if (TicketListView != null) TicketListView.ItemsSource = tickets;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Impossibile caricare i ticket: {ex.Message}");
            }
        }

        // --- GESTORI EVENTI UI ---

        private async void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserListView != null) UserListView.SelectedIndex = -1;
            await LoadTicketsAsync(null);
        }

        private async void UserListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UserListView?.SelectedItem is ItUtente selectedUser)
            {
                await LoadTicketsAsync(selectedUser.Id);
            }
        }

        // --- GESTORI EVENTI TICKET (SALVATAGGIO) ---

        public async void OnTicketStateChanged(object sender, TicketStateChangedEventArgs e)
        {
            await SaveFullTicketStateAsync(e.Nticket);
        }

        public async void OnTicketAssigneeChanged(object sender, TicketAssigneeChangedEventArgs e)
        {
            await SaveFullTicketStateAsync(e.Nticket);
        }

        // Nuovo Handler Generico (per Tipologia e Urgenza)
        public async void OnTicketPropertyChanged(object sender, TicketGenericChangedEventArgs e)
        {
            await SaveFullTicketStateAsync(e.Nticket);
        }

        // --- LOGICA SALVATAGGIO ROBUSTA ---

        // Invece di inviare solo il campo modificato, inviamo lo stato corrente del ViewModel.
        // Questo è necessario perché il Server (come visto nel codice) potrebbe resettare a null
        // l'Assegnatario se non viene specificato nella richiesta JSON.
        private async Task SaveFullTicketStateAsync(int nticket)
        {
            // 1. Trova il ViewModel aggiornato nella lista
            var tickets = TicketListView?.ItemsSource as List<TicketViewModel>;
            var ticket = tickets?.FirstOrDefault(t => t.Nticket == nticket);

            if (ticket == null) return;

            if (RootGrid != null) RootGrid.IsHitTestVisible = false;
            if (LoadingProgressRing != null) { LoadingProgressRing.IsActive = true; LoadingProgressRing.Visibility = Visibility.Visible; }

            try
            {
                string url = $"{_apiBaseUrl}/api/tickets/{nticket}/update";

                // Costruiamo il payload completo per evitare che il server cancelli dati (es. Assegnatario)
                var request = new
                {
                    StatoId = ticket.StatoId,
                    // Convertiamo 0 in null per il DB
                    AssegnatoaId = ticket.AssegnatoaId == 0 ? null : ticket.AssegnatoaId,
                    UrgenzaId = ticket.UrgenzaId,
                    TipologiaId = ticket.TipologiaId
                };

                var response = await _apiClient.PutAsJsonAsync(url, request);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Errore salvataggio: {ex.Message}");
                // Ricarica per ripristinare i dati corretti in caso di errore
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