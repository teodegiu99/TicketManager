using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ClientUser
{
    // Classe di supporto per deserializzare la risposta API { Id, Nome }
    public class ApiItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
    }

    public sealed partial class MainWindow : Window
    {
        private StorageFile? fileScreenshot = null;
        private HttpClient _apiClient;

        // ⚠️ Assicurati che la porta corrisponda alla tua API (es. 5210 o 7186)
        private string _apiBaseUrl = "http://localhost:5210";

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Nuovo Ticket Assistenza";

            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _apiClient = new HttpClient(handler);
        }

        private async void RootPanel_Loaded(object sender, RoutedEventArgs e)
        {
            btnInvia.IsEnabled = false;
            try
            {
                await PopolaComboBoxAsync();
            }
            finally
            {
                btnInvia.IsEnabled = true;
            }
        }

        private async Task PopolaComboBoxAsync()
        {
            try
            {
                // Tipologie e Urgenze restituiscono oggetti [{Id, Nome}]
                await PopolaComboBoxOggetti(cmbTipologia, $"{_apiBaseUrl}/api/tickets/tipologie");
                await PopolaComboBoxOggetti(cmbUrgenza, $"{_apiBaseUrl}/api/tickets/urgenze");

                // Sedi restituisce ancora stringhe ["Sede1", "Sede2"]
                await PopolaComboBoxStringhe(cmbSede, $"{_apiBaseUrl}/api/tickets/sedi");
            }
            catch (Exception ex)
            {
                await MostraDialogo("Errore di Caricamento",
                    $"Impossibile caricare i dati dall'API.\nVerifica che l'API sia avviata.\n\nDettagli: {ex.Message}");
                // Non chiudere l'app brutalmente, lascia che l'utente riprovi o legga l'errore
            }
        }

        // Per endpoint che restituiscono [{ "id": 1, "nome": "..." }]
        private async Task PopolaComboBoxOggetti(ComboBox comboBox, string url)
        {
            try
            {
                var response = await _apiClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var items = JsonSerializer.Deserialize<List<ApiItem>>(json, options);

                comboBox.Items.Clear();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        // Aggiungiamo solo la stringa 'Nome', così SelectedItem rimane stringa
                        // e non rompe la logica di btnInvia_Click
                        comboBox.Items.Add(item.Nome);
                    }
                    if (comboBox.Items.Count > 0) comboBox.SelectedIndex = 0;
                }
            }
            catch { /* Gestione opzionale o ignorata per non bloccare tutto */ }
        }

        // Per endpoint che restituiscono ["Stringa1", "Stringa2"]
        private async Task PopolaComboBoxStringhe(ComboBox comboBox, string url)
        {
            try
            {
                var response = await _apiClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var items = JsonSerializer.Deserialize<List<string>>(json, options);

                comboBox.Items.Clear();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        comboBox.Items.Add(item);
                    }
                    if (comboBox.Items.Count > 0) comboBox.SelectedIndex = 0;
                }
            }
            catch { }
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
                lblFileScelto.Text = $"File: {fileScreenshot.Name}";
            }
        }

        private async void btnInvia_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOggetto.Text) || string.IsNullOrWhiteSpace(txtTesto.Text))
            {
                await MostraDialogo("Errore", "Titolo e Messaggio sono obbligatori.");
                return;
            }

            var content = new MultipartFormDataContent();

            // Qui SelectedItem è una stringa perché abbiamo popolato items con stringhe (item.Nome)
            content.Add(new StringContent(cmbTipologia.SelectedItem?.ToString() ?? ""), "ProblemType");
            content.Add(new StringContent(cmbUrgenza.SelectedItem?.ToString() ?? ""), "Urgency");
            content.Add(new StringContent(txtFunzione.Text ?? ""), "Funzione");
            content.Add(new StringContent(cmbSede.SelectedItem?.ToString() ?? ""), "Sede");
            content.Add(new StringContent(System.Environment.MachineName), "Macchina");
            content.Add(new StringContent(txtOggetto.Text ?? ""), "Title");
            content.Add(new StringContent(txtTesto.Text ?? ""), "Message");

            if (fileScreenshot != null)
            {
                var fileStream = await fileScreenshot.OpenStreamForReadAsync();
                var streamContent = new StreamContent(fileStream);
                content.Add(streamContent, "Screenshot", fileScreenshot.Name);
            }

            try
            {
                btnInvia.IsEnabled = false;
                var response = await _apiClient.PostAsync($"{_apiBaseUrl}/api/tickets", content);

                if (response.IsSuccessStatusCode)
                {
                    await MostraDialogo("Successo", "Ticket inviato con successo!");
                    PulisciCampi();
                }
                else
                {
                    string errore = await response.Content.ReadAsStringAsync();
                    await MostraDialogo("Errore API", $"Stato: {response.StatusCode}\n{errore}");
                }
            }
            catch (Exception ex)
            {
                await MostraDialogo("Errore Grave", $"Connessione fallita: {ex.Message}");
            }
            finally
            {
                btnInvia.IsEnabled = true;
            }
        }

        private void PulisciCampi()
        {
            txtOggetto.Text = "";
            txtTesto.Text = "";
            txtFunzione.Text = "";
            fileScreenshot = null;
            lblFileScelto.Text = "";
            if (cmbTipologia.Items.Count > 0) cmbTipologia.SelectedIndex = 0;
            if (cmbUrgenza.Items.Count > 0) cmbUrgenza.SelectedIndex = 0;
            if (cmbSede.Items.Count > 0) cmbSede.SelectedIndex = 0;
        }

        private async Task MostraDialogo(string titolo, string contenuto)
        {
            if (RootPanel.XamlRoot == null) return;

            ContentDialog dialog = new ContentDialog
            {
                Title = titolo,
                Content = contenuto,
                CloseButtonText = "OK",
                XamlRoot = RootPanel.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void cmbTipologia_SelectedIndexChanged(object sender, SelectionChangedEventArgs e) { }
    }
}