using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ClientIT.Models;

namespace ClientIT
{
    public sealed partial class MainWindow : Window
    {
        private HttpClient _apiClient;
        
        // ⚠️⚠️⚠️ MODIFICA QUESTO URL ⚠️⚠️⚠️
        private string _apiBaseUrl = "http://localhost:5210"; // Esempio! Metti il tuo.

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

        /// <summary>
        /// Carica la lista di utenti IT nella colonna sinistra.
        /// </summary>
        private async Task LoadItUsersAsync()
        {
            var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/auth/users");
            response.EnsureSuccessStatusCode(); // Lancia un'eccezione se l'API fallisce

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var utenti = JsonSerializer.Deserialize<List<ItUtente>>(jsonResponse, options);

            UserListView.ItemsSource = utenti;
        }

        /// <summary>
        /// Carica la lista dei ticket (filtrata o completa) nella colonna destra.
        /// </summary>
        private async Task LoadTicketsAsync(int? userId = null)
        {
            LoadingProgressRing.IsActive = true;
            LoadingProgressRing.Visibility = Visibility.Visible;
            TicketListView.ItemsSource = null; // Svuota la lista precedente

            try
            {
                string url = $"{_apiBaseUrl}/api/tickets/all";

                // Se è stato fornito un userID, aggiungilo come filtro
                if (userId.HasValue)
                {
                    url += $"?assegnatoaId={userId.Value}";
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