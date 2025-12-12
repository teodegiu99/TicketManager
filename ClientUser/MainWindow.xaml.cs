using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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

    // DTO per visualizzare i ticket
    public class TicketDto
    {
        public int Nticket { get; set; }
        public string Titolo { get; set; } = string.Empty;
        public string Testo { get; set; } = string.Empty;
        public DateTime DataCreazione { get; set; }

        public string StatoNome { get; set; } = string.Empty;
        public string UrgenzaNome { get; set; } = string.Empty;
        public string TipologiaNome { get; set; } = string.Empty;
        public string SedeNome { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public string? PerContoDi { get; set; }

        public string? Note { get; set; }
        public string? ScreenshotPath { get; set; }
        public string? Macchina { get; set; }
        public string? Funzione { get; set; }

        public string DataCreazioneFormatted => DataCreazione.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }

    public sealed partial class MainWindow : Window
    {
        private StorageFile? fileScreenshot = null;
        private HttpClient _apiClient;
        private string _apiBaseUrl = "http://localhost:5210";
        private List<string> _allAdUsers = new();

        // 1. Variabile per il Timer
        private DispatcherTimer _autoRefreshTimer;

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

            // 2. Configurazione e Avvio del Timer (5 Minuti)
            _autoRefreshTimer = new DispatcherTimer();
            _autoRefreshTimer.Interval = TimeSpan.FromMinutes(5);
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            _autoRefreshTimer.Start();
        }

        // 3. Evento che scatta ogni 5 minuti
        private async void AutoRefreshTimer_Tick(object sender, object e)
        {
            // Ricarica la lista silenziosamente
            await LoadMyTickets();
        }

        // 4. Metodo Pubblico per il Bottone Refresh (da collegare con x:Bind)
        public async void RefreshTickets()
        {
            await LoadMyTickets();
        }

        private async void RootPanel_Loaded(object sender, RoutedEventArgs e)
        {
            btnInvia.IsEnabled = false;
            try
            {
                await PopolaComboBoxAsync();
                await CaricaUtentiAdAsync();
                await LoadMyTickets();
            }
            finally
            {
                btnInvia.IsEnabled = true;
            }
        }

        private async void MyTicketsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TicketDto ticket)
            {
                var detailContent = new TicketDetailDialog(ticket);

                var dialog = new ContentDialog
                {
                    Title = "Dettaglio Ticket",
                    Content = detailContent,
                    CloseButtonText = "Chiudi",
                    XamlRoot = this.Content.XamlRoot,
                    DefaultButton = ContentDialogButton.Close
                };

                await dialog.ShowAsync();
            }
        }

        // --- GESTIONE LISTA TICKET PERSONALI ---

        private async Task LoadMyTickets()
        {
            if (ListLoader != null)
            {
                ListLoader.Visibility = Visibility.Visible;
                ListLoader.IsActive = true;
            }

            try
            {
                string url = $"{_apiBaseUrl}/api/tickets/all?mine=true";
                var tickets = await _apiClient.GetFromJsonAsync<List<TicketDto>>(url);

                if (MyTicketsList != null)
                {
                    MyTicketsList.ItemsSource = tickets;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore caricamento ticket: {ex.Message}");
            }
            finally
            {
                if (ListLoader != null)
                {
                    ListLoader.IsActive = false;
                    ListLoader.Visibility = Visibility.Collapsed;
                }
            }
        }

        public string FormatDate(DateTime dt) => dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        // --- CARICAMENTO DATI ---

        private async Task CaricaUtentiAdAsync()
        {
            try
            {
                var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/auth/ad-users-list");
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var users = JsonSerializer.Deserialize<List<string>>(json, options);
                    if (users != null) _allAdUsers = users;
                }
            }
            catch { }
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
                await MostraDialogo("Errore di Caricamento", $"Impossibile connettersi all'API: {ex.Message}");
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
                    foreach (var item in items) comboBox.Items.Add(item.Nome);
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
                    foreach (var item in items) comboBox.Items.Add(item);
                    if (comboBox.Items.Count > 0) comboBox.SelectedIndex = 0;
                }
            }
            catch { }
        }

        // --- GESTIONE AUTOSUGGESTBOX ---

        private void asbPerContoDi_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var query = sender.Text.ToLower();
                if (string.IsNullOrWhiteSpace(query)) sender.ItemsSource = _allAdUsers;
                else sender.ItemsSource = _allAdUsers.Where(u => u.ToLower().Contains(query)).ToList();
            }
        }

        private void asbPerContoDi_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem != null) sender.Text = args.SelectedItem.ToString();
        }

        private void asbPerContoDi_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is AutoSuggestBox box)
            {
                if (_allAdUsers != null && _allAdUsers.Any())
                {
                    box.ItemsSource = _allAdUsers;
                    box.IsSuggestionListOpen = true;
                }
            }
        }

        // --- INVIO TICKET ---

        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(".jpg");
            filePicker.FileTypeFilter.Add(".png");
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(filePicker, hwnd);
            fileScreenshot = await filePicker.PickSingleFileAsync();
            if (fileScreenshot != null) lblFileScelto.Text = $"File: {fileScreenshot.Name}";
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
                    await LoadMyTickets();
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
            asbPerContoDi.Text = "";
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