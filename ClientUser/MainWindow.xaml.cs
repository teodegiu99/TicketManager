using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ClientUser
{
    public class ApiItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
    }

    public sealed partial class MainWindow : Window
    {
        private StorageFile? fileScreenshot = null;
        private HttpClient _apiClient;

        // Assicurati che la porta corrisponda a quella definita in API/Properties/launchSettings.json
        private string _apiBaseUrl = "http://localhost:5210";

        // Cache per la lista completa degli utenti AD
        private List<string> _allAdUsers = new();

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
                // Carica le liste per i ComboBox (Tipologia, Urgenza, Sede)
                await PopolaComboBoxAsync();

                // Carica la lista utenti AD per il campo "Per Conto Di"
                await CaricaUtentiAdAsync();
            }
            finally
            {
                btnInvia.IsEnabled = true;
            }
        }

        // --- CARICAMENTO DATI ---

        private async Task CaricaUtentiAdAsync()
        {
            try
            {
                // Chiama il nuovo endpoint creato nell'AuthController
                var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/auth/ad-users-list");

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var users = JsonSerializer.Deserialize<List<string>>(json, options);

                    if (users != null)
                    {
                        _allAdUsers = users;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Impossibile caricare utenti AD: {ex.Message}");
                // Non mostriamo errori bloccanti all'utente, il campo funzionerà semplicemente come testo libero
            }
        }

        private async Task PopolaComboBoxAsync()
        {
            try
            {
                await PopolaComboBoxOggetti(cmbTipologia, $"{_apiBaseUrl}/api/tickets/tipologie");
                await PopolaComboBoxOggetti(cmbUrgenza, $"{_apiBaseUrl}/api/tickets/urgenze");
                await PopolaComboBoxStringhe(cmbSede, $"{_apiBaseUrl}/api/tickets/sedi");
            }
            catch (Exception ex)
            {
                await MostraDialogo("Errore di Caricamento",
                    $"Impossibile caricare i dati dall'API.\nVerifica che l'API sia avviata.\n\nDettagli: {ex.Message}");
            }
        }

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
                        comboBox.Items.Add(item.Nome);
                    }
                    if (comboBox.Items.Count > 0) comboBox.SelectedIndex = 0;
                }
            }
            catch { }
        }

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

        // --- GESTIONE AUTOSUGGESTBOX (PER CONTO DI) ---

        private void asbPerContoDi_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Filtra solo se è l'utente a digitare
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var query = sender.Text.ToLower();

                if (string.IsNullOrWhiteSpace(query))
                {
                    // Se vuoto, mostra l'intera lista
                    sender.ItemsSource = _allAdUsers;
                }
                else
                {
                    // Filtra la lista in memoria
                    var filtered = _allAdUsers
                        .Where(u => u.ToLower().Contains(query))
                        .ToList();

                    sender.ItemsSource = filtered;
                }
            }
        }

        private void asbPerContoDi_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            // Quando l'utente clicca un suggerimento, imposta il testo
            if (args.SelectedItem != null)
            {
                sender.Text = args.SelectedItem.ToString();
            }
        }

        private void asbPerContoDi_GotFocus(object sender, RoutedEventArgs e)
        {
            // FIX APPLICATO: Casting di sender a AutoSuggestBox
            if (sender is AutoSuggestBox box)
            {
                // Quando clicco nella casella, se ho utenti caricati, mostro la lista
                if (_allAdUsers != null && _allAdUsers.Any())
                {
                    // Resetta la sorgente alla lista completa se non c'è testo o per refreshare
                    box.ItemsSource = _allAdUsers;
                    box.IsSuggestionListOpen = true;
                }
            }
        }

        // --- GESTIONE INTERFACCIA E INVIO ---

        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(".jpg");
            filePicker.FileTypeFilter.Add(".png");

            // Necessario per WinUI 3 desktop
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

            content.Add(new StringContent(cmbTipologia.SelectedItem?.ToString() ?? ""), "ProblemType");
            content.Add(new StringContent(cmbUrgenza.SelectedItem?.ToString() ?? ""), "Urgency");
            content.Add(new StringContent(txtFunzione.Text ?? ""), "Funzione");
            content.Add(new StringContent(cmbSede.SelectedItem?.ToString() ?? ""), "Sede");
            content.Add(new StringContent(System.Environment.MachineName), "Macchina");
            content.Add(new StringContent(txtOggetto.Text ?? ""), "Title");
            content.Add(new StringContent(txtTesto.Text ?? ""), "Message");

            // INVIO DEL CAMPO PER CONTO DI
            content.Add(new StringContent(asbPerContoDi.Text ?? ""), "PerContoDi");

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
            asbPerContoDi.Text = ""; // Pulisci anche il nuovo campo

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

        private void cmbTipologia_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtFunzione == null || cmbTipologia == null) return;

            if (cmbTipologia.SelectedItem is string selezione)
            {
                // Se contiene "protex" (case-insensitive), mostra il campo
                if (selezione.Contains("protex", StringComparison.OrdinalIgnoreCase))
                {
                    txtFunzione.Visibility = Visibility.Visible;
                }
                else
                {
                    txtFunzione.Visibility = Visibility.Collapsed;
                    txtFunzione.Text = string.Empty;
                }
            }
            else
            {
                txtFunzione.Visibility = Visibility.Collapsed;
            }
        }
    }
}