using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClientIT
{
    public sealed partial class MainWindow : Window
    {
        private HttpClient _apiClient;

        // ⚠️⚠️⚠️ MODIFICA QUESTO URL ⚠️⚠️⚠️
        // Metti l'URL base della tua API (lo stesso di ClientUser)
        private string _apiBaseUrl = "http://localhost:5210"; // Esempio! Metti il tuo.

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Gestione Ticket (IT)";

            // Inizializza il client HTTP (necessario anche qui per caricare i dati)
            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                // Per test in locale, bypassa controllo certificato (se usi HTTPS)
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _apiClient = new HttpClient(handler);

            // Carichiamo i dati quando la finestra è attivata per la prima volta
            this.Activated += MainWindow_Activated;
        }

        private bool _isFirstActivation = true;

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            // Esegui il caricamento solo al primo activation
            if (_isFirstActivation && e.WindowActivationState != WindowActivationState.Deactivated)
            {
                _isFirstActivation = false;
                await LoadDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            // 1. Imposta il messaggio di benvenuto
            // Recuperiamo l'utente che si è loggato (salvato in App.xaml.cs)
            var currentUser = App.CurrentUser;
            if (currentUser != null)
            {
                WelcomeMessage.Text = $"Bentornato, {currentUser.UsernameAd} (Livello: {currentUser.Permesso})";
            }
            else
            {
                WelcomeMessage.Text = "Benvenuto, utente non riconosciuto.";
            }

            // 2. Carica i dati nelle due colonne
            // Usa un ProgressRing o disabilita i controlli specifici invece del Grid
            LoadingProgressRing.IsActive = true;
            LoadingProgressRing.Visibility = Visibility.Visible;

            try
            {
                await LoadItUsersAsync();
                await LoadAllTicketsAsync();
            }
            catch (HttpRequestException ex)
            {
                // Errore di rete o API non raggiungibile
                await ShowErrorDialogSafe($"Errore di connessione all'API: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Altri errori generici
                await ShowErrorDialogSafe($"Errore nel caricamento dati: {ex.Message}");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Carica la lista di utenti IT nella colonna sinistra.
        /// </summary>
        private async Task LoadItUsersAsync()
        {
            var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/auth/users");

            // Gestisci errori HTTP specifici
            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = $"Errore API (Status: {response.StatusCode})";
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    errorMessage = "Accesso negato: utente non abilitato.";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    errorMessage = "Autenticazione fallita.";
                }
                throw new HttpRequestException(errorMessage);
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var utenti = JsonSerializer.Deserialize<List<ItUtente>>(jsonResponse, options);

            UserListView.ItemsSource = utenti;
        }

        /// <summary>
        /// Carica la lista di tutti i ticket nella colonna destra.
        /// </summary>
        private async Task LoadAllTicketsAsync()
        {
            var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/tickets/all");

            // Gestisci errori HTTP specifici
            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = $"Errore nel caricamento ticket (Status: {response.StatusCode})";
                throw new HttpRequestException(errorMessage);
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var tickets = JsonSerializer.Deserialize<List<TicketViewModel>>(jsonResponse, options);

            TicketListView.ItemsSource = tickets;
        }

        /// <summary>
        /// Mostra un popup di errore in modo sicuro, verificando che XamlRoot sia disponibile.
        /// </summary>
        private async Task ShowErrorDialogSafe(string content)
        {
            try
            {
                // Verifica che il Content e il XamlRoot siano disponibili
                if (this.Content == null || this.Content.XamlRoot == null)
                {
                    // Fallback: scrivi nel messaggio di benvenuto se il dialog non può essere mostrato
                    if (WelcomeMessage != null)
                    {
                        WelcomeMessage.Text = $"ERRORE: {content}";
                    }
                    return;
                }

                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Errore",
                    Content = content,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                // Ultimo fallback: mostra nel messaggio di benvenuto
                if (WelcomeMessage != null)
                {
                    WelcomeMessage.Text = $"ERRORE: {content} (Dialog failed: {ex.Message})";
                }
            }
        }
    }
}