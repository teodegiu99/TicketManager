using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClientIT
{
    public sealed partial class MainWindow : Window
    {
        private HttpClient _apiClient;
        
        // ⚠️⚠️⚠️ MODIFICA QUESTO URL ⚠️⚠️⚠️
        private string _apiBaseUrl = "http://localhost:5210"; // Esempio! Metti il tuo.

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

            // Agganciamo l'evento Loaded per caricare i dati
            this.Activated += MainWindow_Activated;
        }       

        // Modifica la firma del gestore evento:
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            // Chiamata asincrona corretta
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            // 1. Imposta il messaggio di benvenuto
            var currentUser = App.CurrentUser; // Recupera l'utente da App.xaml.cs
            if (currentUser != null)
            {
                WelcomeMessage.Text = $"Bentornato, {currentUser.UsernameAd} (Livello: {currentUser.Permesso})";
            }
            else
            {
                WelcomeMessage.Text = "Benvenuto, utente non riconosciuto.";
            }

            // 2. Mostra il caricamento
            LoadingProgressRing.IsActive = true;
            LoadingProgressRing.Visibility = Visibility.Visible;
            RootGrid.IsHitTestVisible = false; // Disabilita l'intera griglia

            try
            {
                // Carica la lista utenti (a sinistra)
                await LoadItUsersAsync();
                await LoadStatiAsync();
                // Carica tutti i ticket (a destra) all'avvio
                await LoadTicketsAsync(); // Carica senza filtro
            }
            catch (Exception ex)
            {
                // Gestisce errori di rete o API
                await ShowErrorDialog($"Errore nel caricamento dati: {ex.Message}");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
                RootGrid.IsHitTestVisible = true; // Riabilita l'intera griglia
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
                foreach (var stato in stati)
                {
                    AllStati.Add(stato);
                }
            }
        }
        /// <summary>
        /// Carica la lista di utenti IT nella colonna sinistra.
        /// </summary>
        private async Task LoadItUsersAsync()
        {
            var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/auth/users");
            response.EnsureSuccessStatusCode();

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var utenti = JsonSerializer.Deserialize<List<ItUtente>>(jsonResponse, options);

            // Popola la lista a sinistra
            UserListView.ItemsSource = utenti;

            // --- ECCO LA LOGICA MANCANTE ---
            // Popola la lista per il ComboBox (aggiungendo "Non assegnato")
            AllItUsers.Clear();
            AllItUsers.Add(ItUtente.NonAssegnato); // Aggiunge l'opzione "Non assegnato" (ID 0)
            if (utenti != null)
            {
                foreach (var utente in utenti)
                {
                    AllItUsers.Add(utente);
                }
            }
            // --- FINE LOGICA MANCANTE ---
        }

        /// <summary>
        /// Carica la lista dei ticket (filtrata o completa) nella colonna destra.
        /// </summary>
        private async Task LoadTicketsAsync(int? assegnatoa_id = null)
        {
            LoadingProgressRing.IsActive = true;
            LoadingProgressRing.Visibility = Visibility.Visible;
            TicketListView.ItemsSource = null; // Svuota la lista precedente

            try
            {
                string url = $"{_apiBaseUrl}/api/tickets/all";

                // Se è stato fornito un ID, aggiungilo come filtro
                if (assegnatoa_id.HasValue)
                {
                    url += $"?assegnatoa_id={assegnatoa_id.Value}";
                }

                var response = await _apiClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var tickets = JsonSerializer.Deserialize<List<TicketViewModel>>(jsonResponse, options);

                TicketListView.ItemsSource = tickets;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Impossibile caricare i ticket: {ex.Message}");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Chiamato quando l'utente clicca su "Mostra Tutti".
        /// </summary>
        private async void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            UserListView.SelectedItem = null; // Deseleziona la lista
            await LoadTicketsAsync(); // Carica tutti i ticket
        }
        /// <summary>
        /// (NUOVO) Auto-Salvataggio quando si cambia lo STATO
        /// </summary>
        private async void StatoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            // Troviamo il Nticket (ID) del ticket che abbiamo salvato nel Tag
            // e ci assicuriamo che l'evento non sia causato da un binding iniziale
            if (comboBox?.Tag is int nticket &&
                comboBox.SelectedValue is int statoId &&
                e.AddedItems.Count > 0)
            {
                await UpdateTicketAsync(nticket, statoId, null);
            }
        }

        /// <summary>
        /// (NUOVO) Auto-Salvataggio quando si cambia l'ASSEGNATARIO
        /// </summary>
        private async void AssegnatoaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.Tag is int nticket &&
                comboBox.SelectedValue is int assegnatoaId &&
                e.AddedItems.Count > 0)
            {
                await UpdateTicketAsync(nticket, null, assegnatoaId);
            }
        }

        /// <summary>
        /// (NUOVO) Chiama l'endpoint PUT dell'API per l'auto-salvataggio.
        /// </summary>
        private async Task UpdateTicketAsync(int nticket, int? statoId, int? assegnatoaId)
        {
            LoadingProgressRing.IsActive = true;
            LoadingProgressRing.Visibility = Visibility.Visible;
            RootGrid.IsHitTestVisible = false; // Blocca l'interfaccia durante il salvataggio

            try
            {
                // Costruisce l'URL (es. /api/tickets/123/update)
                string url = $"{_apiBaseUrl}/api/tickets/{nticket}/update";

                // Crea l'oggetto da inviare (solo con i campi che vogliamo cambiare)
                var request = new
                {
                    StatoId = statoId,
                    AssegnatoaId = assegnatoaId
                };

                // Invia la richiesta PUT
                var response = await _apiClient.PutAsJsonAsync(url, request);
                response.EnsureSuccessStatusCode();

                // Auto-salvataggio riuscito.
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Errore during l'auto-salvataggio: {ex.Message}");
                // Se il salvataggio fallisce, ricarichiamo la lista per annullare la modifica
                await LoadTicketsAsync();
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
                RootGrid.IsHitTestVisible = true; // Sblocca l'interfaccia
            }
        }

        /// <summary>
        /// Chiamato quando l'utente seleziona un nome dalla lista.
        /// </summary>
        private async void UserListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ottieni l'utente selezionato
            var selectedUser = UserListView.SelectedItem as ItUtente;
            
            // Se la selezione è stata cancellata (es. da "Mostra Tutti"), non fare nulla
            if (selectedUser == null)
            {
                return;
            }

            // Carica i ticket filtrando per l'utente selezionato (passando l'ID)
            await LoadTicketsAsync(selectedUser.Id);
        }

        /// <summary>
        /// Mostra un popup di errore in modo sicuro, usando il XamlRoot corretto.
        /// </summary>
        private async Task ShowErrorDialog(string content)
        {
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Errore",
                Content = content,
                CloseButtonText = "OK",
                // Usiamo XamlRoot della griglia principale (RootGrid)
                XamlRoot = RootGrid.XamlRoot 
            };
            await errorDialog.ShowAsync();
        }
    }
}