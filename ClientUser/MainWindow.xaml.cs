using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic; // Per List<string>
using System.IO;
using System.Net.Http;
using System.Text.Json; // Per il JSON
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ClientUser
{
    public sealed partial class MainWindow : Window
    {
        private StorageFile? fileScreenshot = null; // Reso Nullable
        private HttpClient _apiClient;

        // ⚠️ Ricorda di impostare l'URL corretto
        private string _apiBaseUrl = "http://localhost:5210";

        public MainWindow()
        {
            this.InitializeComponent(); // Ora questo funzionerà
            this.Title = "Nuovo Ticket Assistenza";

            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback =
                    (sender, cert, chain, sslPolicyErrors) => true
            };
            _apiClient = new HttpClient(handler);
        }

        // --- SOLUZIONE (Aggiramento) ---
        // Abbiamo sostituito 'RootPanel' con '(sender as FrameworkElement)'
        private async void RootPanel_Loaded(object sender, RoutedEventArgs e)
        {
            // Disabilita solo il pulsante di invio durante il caricamento
            btnInvia.IsEnabled = false;
            
            try
            {
                await PopolaComboBoxAsync();
            }
            finally
            {
                // Riabilita il pulsante al termine
                btnInvia.IsEnabled = true;
            }
        }


        // Nuovo metodo ASYNC per popolare le ComboBox dall'API
        private async Task PopolaComboBoxAsync()
        {
            try
            {
                // Popola Tipologia
                await PopolaSingolaComboBox(cmbTipologia, $"{_apiBaseUrl}/api/tickets/tipologie");
                 
                // Popola Urgenza
                await PopolaSingolaComboBox(cmbUrgenza, $"{_apiBaseUrl}/api/tickets/urgenze");

                // Popola Sede
                await PopolaSingolaComboBox(cmbSede, $"{_apiBaseUrl}/api/tickets/sedi");
            }
            catch (Exception ex)
            {
                await MostraDialogo("Errore di Caricamento",
                    $"Impossibile caricare i dati dall'API.\nVerifica che l'API sia in esecuzione e l'URL sia corretto.\n\nDettagli: {ex.Message}");
                this.Close();
            }
        }

        // Funzione helper per caricare i dati in una ComboBox
        private async Task PopolaSingolaComboBox(ComboBox comboBox, string url)
        {
            var response = await _apiClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string jsonResponse = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var opzioni = JsonSerializer.Deserialize<List<string>>(jsonResponse, options);

            if (opzioni != null)
            {
                comboBox.Items.Clear();
                foreach (var opzione in opzioni)
                {
                    comboBox.Items.Add(opzione);
                }
                if (comboBox.Items.Count > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
            }
        }


        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(".jpg");
            filePicker.FileTypeFilter.Add(".png");

            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(filePicker, hwnd);

            fileScreenshot = await filePicker.PickSingleFileAsync();

            if (fileScreenshot != null)
            {
                lblFileScelto.Text = $"File selezionato: {fileScreenshot.Name}";
            }
        }

        private async void btnInvia_Click(object sender, RoutedEventArgs e)
        {
            // Validazione
            if (string.IsNullOrWhiteSpace(txtOggetto.Text) ||
                string.IsNullOrWhiteSpace(txtTesto.Text))
            {
                await MostraDialogo("Errore", "Titolo e Messaggio sono obbligatori.");
                return;
            }

            var content = new MultipartFormDataContent();

            // Correzione Nullable
            content.Add(new StringContent(cmbTipologia.SelectedItem?.ToString() ?? ""), "ProblemType");
            content.Add(new StringContent(cmbUrgenza.SelectedItem?.ToString() ?? ""), "Urgency");
            content.Add(new StringContent(txtFunzione.Text ?? ""), "Funzione");
            content.Add(new StringContent(cmbSede.SelectedItem?.ToString() ?? ""), "Sede");
            content.Add(new StringContent(System.Environment.MachineName ?? "Sconosciuto"), "Macchina");
            content.Add(new StringContent(txtOggetto.Text ?? ""), "Title");
            content.Add(new StringContent(txtTesto.Text ?? ""), "Message");

            if (fileScreenshot != null)
            {
                var fileStream = await fileScreenshot.OpenStreamForReadAsync();
                var streamContent = new StreamContent(fileStream);
                content.Add(streamContent, "Screenshot", fileScreenshot.Name);
            }

            // --- Logica di invio all'API ---
            try
            {
                btnInvia.IsEnabled = false;
                string apiUrl = $"{_apiBaseUrl}/api/tickets";
                var response = await _apiClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    await MostraDialogo("Successo", "Ticket inviato con successo!");
                    // Pulisci i campi
                    txtOggetto.Text = "";
                    txtTesto.Text = "";
                    txtFunzione.Text = "";
                    fileScreenshot = null;
                    lblFileScelto.Text = "";
                    cmbTipologia.SelectedIndex = 0;
                    cmbUrgenza.SelectedIndex = 0;
                    cmbSede.SelectedIndex = 0;
                }
                else
                {
                    string errore = await response.Content.ReadAsStringAsync();
                    await MostraDialogo("Errore API", $"Errore: {response.StatusCode}\n{errore}");
                }
            }
            catch (Exception ex)
            {
                await MostraDialogo("Errore Grave", $"Errore di connessione: {ex.Message}");
            }
            finally
            {
                btnInvia.IsEnabled = true;
            }
        }

        // Helper per mostrare i dialoghi
        private async Task MostraDialogo(string titolo, string contenuto)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = titolo,
                Content = contenuto,
                CloseButtonText = "OK",
                // 'RootPanel' qui funzionerà, perché questo metodo
                // viene chiamato molto dopo il 'Loaded'.
                XamlRoot = RootPanel.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void cmbTipologia_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            // Puoi lasciare questo vuoto
        }
    }
}