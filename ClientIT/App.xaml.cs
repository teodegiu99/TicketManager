using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System;

namespace ClientIT
{
    public partial class App : Application
    {
        private Window m_window;
        private HttpClient _apiClient;

        // ⚠️⚠️⚠️ MODIFICA QUESTO URL ⚠️⚠️⚠️
        // Metti l'URL base della tua API (lo stesso di ClientUser)
        private string _apiBaseUrl = "http://localhost:5210";

        // Salveremo qui i dati dell'utente che ha effettuato l'accesso
        public static ItAuthData? CurrentUser { get; private set; } // Reso Nullable

        public App()
        {
            // Gestisci le eccezioni non gestite PRIMA di InitializeComponent
            this.UnhandledException += (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"UnhandledException catturata: {e.Exception}");
                // Marca l'eccezione come gestita per evitare il crash
                e.Handled = true;
            };

            this.InitializeComponent();

            // Inizializza il client HTTP con le credenziali AD
            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _apiClient = new HttpClient(handler);
        }

        /// <summary>
        /// Questo metodo viene chiamato all'avvio.
        /// È stato modificato per eseguire l'autenticazione prima di mostrare la finestra.
        /// </summary>
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                // Esegui il check di autenticazione/autorizzazione
                bool isAuthorized = await TryAuthenticateAsync();

                if (isAuthorized)
                {
                    // Utente autorizzato: mostra la finestra principale
                    m_window = new MainWindow();
                    m_window.Activate();
                }
                else
                {
                    // Utente non autorizzato: mostra un errore e chiudi
                    await ShowErrorDialogAndExit();
                }
            }
            catch (Exception ex)
            {
                // Gestisci eventuali eccezioni non previste
                await ShowErrorDialogAndExit($"Errore inaspettato: {ex.Message}");
            }
        }

        /// <summary>
        /// Chiama l'endpoint 'api/auth/check' per verificare i permessi dell'utente.
        /// </summary>
        private async Task<bool> TryAuthenticateAsync()
        {
            try
            {
                // Chiama l'endpoint che abbiamo creato nell'API
                var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/auth/check");

                if (response.IsSuccessStatusCode)
                {
                    // L'API ha risposto 200 OK. Leggiamo i dati dell'utente.
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    // Salva i dati dell'utente (permessi, ID, ecc.)
                    var deserializedUser = JsonSerializer.Deserialize<ItAuthData>(jsonResponse, options);
                    if (deserializedUser != null)
                    {
                        CurrentUser = deserializedUser;
                        return true;
                    }
                    return false;
                }

                // Se l'API risponde 403 (Forbid), l'utente è autenticato (AD)
                // ma non è nella tabella it_utenti.
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    // Logica di gestione accesso negato (già gestita nel 'else' di OnLaunched)
                    return false;
                }

                // Altri errori (es. 500, 404)
                return false;
            }
            catch (HttpRequestException ex)
            {
                // L'API è spenta, errore di connessione, o URL sbagliato
                System.Diagnostics.Debug.WriteLine($"Errore di connessione API: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                // Altre eccezioni inaspettate
                System.Diagnostics.Debug.WriteLine($"Errore durante autenticazione: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mostra un popup di errore (ContentDialog) in modo asincrono.
        /// Dato che la MainWindow non esiste, creiamo un popup "fittizio".
        /// </summary>
        private async Task ShowErrorDialogAndExit(string messageOverride = null)
        {
            try
            {
                // Creiamo una finestra "invisibile" solo per mostrare il dialogo
                m_window = new Window();
                
                // Assegna un contenuto fittizio per ottenere un XamlRoot
                m_window.Content = new Grid();
                
                // Attiva la finestra
                m_window.Activate();

                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Accesso Negato",
                    Content = messageOverride ?? "Non sei autorizzato ad utilizzare questa applicazione.\nVerifica che l'API ('TicketsAPI') sia in esecuzione e di essere presente nella tabella 'it_utenti'.",
                    CloseButtonText = "Chiudi",
                    XamlRoot = m_window.Content.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore nella visualizzazione del dialogo: {ex.Message}");
            }
            finally
            {
                // L'app si chiuderà quando l'utente preme "Chiudi" sul popup
                Application.Current.Exit();
            }
        }
    }
}
