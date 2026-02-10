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
using TicketManager;
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
        public int StatoId { get; set; }
        public string StatoNome { get; set; } = string.Empty;
        public string UrgenzaNome { get; set; } = string.Empty;
        public string TipologiaNome { get; set; } = string.Empty;
        public string SedeNome { get; set; } = string.Empty;
        public string AssegnatoaNome { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? PerContoDi { get; set; }

        public string? Note { get; set; }
        public string? Macchina { get; set; }
        public string? Funzione { get; set; }

        public int SollecitiCount { get; set; }
        public List<string> ScreenshotPaths { get; set; } = new List<string>();
        public string DataCreazioneFormatted => DataCreazione.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }

    public sealed partial class MainWindow : Window
    {
        private List<StorageFile> _selectedScreenshots = new();
        private HttpClient _apiClient;
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

            _autoRefreshTimer = new DispatcherTimer();
            _autoRefreshTimer.Interval = TimeSpan.FromSeconds(30); // Era FromMinutes(5)
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            _autoRefreshTimer.Start();
        }
        private async void swShowClosed_Toggled(object sender, RoutedEventArgs e)
        {
            // Ricarica la lista quando si cambia lo switch
            await LoadMyTickets();
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
                    Title = $"Dettaglio Ticket #{ticket.Nticket}", // Ho aggiunto il numero ticket nel titolo per chiarezza
                    Content = detailContent,
                    CloseButtonText = "Indietro",
                    // --- AGGIUNTA TASTO SOLLECITA ---
                    SecondaryButtonText = "Sollecita",
                    XamlRoot = this.Content.XamlRoot,
                    DefaultButton = ContentDialogButton.Close
                };
              
                // --- MODIFICA 3: Logica Bottone Chiudi Ticket ---
                // Mostriamo il tasto "Chiudi" solo se il ticket non è già chiuso (Stato != "Chiuso" o id 3)
                // Nota: Verifichiamo lo stato tramite stringa o id se disponibile nel DTO
                if (!ticket.StatoNome.Equals("Terminato", StringComparison.OrdinalIgnoreCase))
                {
                    dialog.PrimaryButtonText = "Chiudi Ticket";
                }

                bool confirmationRequested = false;

                // Gestione Chiusura Ticket (Primary Button)
                dialog.PrimaryButtonClick += async (s, args) =>
                {
                    args.Cancel = true; // Non chiudere subito il dialog per gestire feedback
                    var d = (ContentDialog)s;

                    if (!confirmationRequested)
                    {
                        d.Title = "Sei sicuro?";
                        d.Content = "Vuoi chiudere definitivamente questo ticket?";
                        d.PrimaryButtonText = "Conferma Chiusura";
                        d.SecondaryButtonText = ""; // Nasconde il sollecito durante la conferma
                        confirmationRequested = true;
                        return;
                    }

                    try
                    {
                        // Payload per l'aggiornamento stato (StatoId 3 = Chiuso)
                        var updateRequest = new { StatoId = 3, Note = "Chiuso dall'utente" };

                        // Chiamata API PUT
                        var response = await _apiClient.PutAsJsonAsync($"{ApiConfig.BaseUrl}/api/tickets/{ticket.Nticket}/update", updateRequest);

                        if (response.IsSuccessStatusCode)
                        {
                            d.Title = "✅ Ticket Chiuso";
                            d.Content = "Il ticket è stato chiuso con successo.";
                            d.PrimaryButtonText = ""; // Nasconde bottone
                            d.IsSecondaryButtonEnabled = false;

                            // Forza refresh lista immediato
                            await LoadMyTickets();

                            // Chiude il dialog dopo 1 secondo
                            await Task.Delay(1000);
                            args.Cancel = false;
                        }
                        else
                        {
                            d.Title = "❌ Errore";
                            d.Content = "Impossibile chiudere il ticket.";
                        }
                    }
                    catch
                    {
                        d.Title = "❌ Errore Connessione";
                    }
                };

                // Gestiamo il click del tasto "Sollecita"
                dialog.SecondaryButtonClick += async (s, args) =>
                {
                    // Evitiamo che il dialogo si chiuda subito (opzionale, ma utile per dare feedback)
                    args.Cancel = true;
                    var d = (ContentDialog)s;
                    if (ticket.SollecitiCount > 0 && !confirmationRequested)
                    {
                        d.Title = $"⚠️ Ticket già sollecitato ({ticket.SollecitiCount} volte)";
                        d.SecondaryButtonText = "Conferma di nuovo";
                        // Impostiamo il flag così al prossimo click entra nel blocco "try" sotto
                        confirmationRequested = true;
                        return;
                    }
                    d.IsSecondaryButtonEnabled = false; // Evita doppi click

                    try
                    {
                        // Chiamata all'API
                        var response = await _apiClient.PostAsync($"{ApiConfig.BaseUrl}/api/tickets/{ticket.Nticket}/sollecita", null);

                        if (response.IsSuccessStatusCode)
                        {
                            // Feedback visivo semplice modificando il titolo o mostrando un altro dialog
                            d.Title = $"✅ Sollecito inviato! (Ticket #{ticket.Nticket})";
                            d.SecondaryButtonText = "Inviato";
                            ticket.SollecitiCount++;
                        }
                        else
                        {
                            d.Title = $"❌ Errore sollecito (Ticket #{ticket.Nticket})";
                        }
                    }
                    catch (Exception)
                    {
                        d.Title = "❌ Errore di connessione";
                    }
                    finally
                    {
                        // Riabilita il bottone dopo un po' o lascialo disabilitato
                        // d.IsSecondaryButtonEnabled = true; 
                    }
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
                // 1. Costruiamo l'URL base
                string url = $"{ApiConfig.BaseUrl}/api/tickets/all?mine=true";

                // 2. Se lo switch "Mostra terminati" è attivo, aggiungiamo il parametro
                if (swShowClosed != null && swShowClosed.IsOn)
                {
                    url += "&includeAll=true";
                }

                var tickets = await _apiClient.GetFromJsonAsync<List<TicketDto>>(url);

                if (MyTicketsList != null && tickets != null)
                {
                    // 3. Logica di Ordinamento e Raggruppamento

                    // A. Ticket APERTI (StatoId != 3)
                    // Li manteniamo nell'ordine dato dal server (che è per Urgenza)
                    var openTickets = tickets
                        .Where(t => t.StatoId != 3)
                        .ToList();

                    // B. Ticket CHIUSI (StatoId == 3)
                    // Li ordiniamo per data decrescente (dal più recente al più vecchio)
                    var closedTickets = tickets
                        .Where(t => t.StatoId == 3)
                        .OrderByDescending(t => t.DataCreazione)
                        .ToList();

                    // C. Uniamo le due liste
                    var finalList = new List<TicketDto>();
                    finalList.AddRange(openTickets);
                    finalList.AddRange(closedTickets);

                    // Assegniamo la lista ordinata alla ListView
                    MyTicketsList.ItemsSource = finalList;
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
                var response = await _apiClient.GetAsync($"{ApiConfig.BaseUrl}/api/auth/ad-users-list");
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
                await PopolaComboBoxOggetti(cmbTipologia, $"{ApiConfig.BaseUrl}/api/tickets/tipologie");
                await PopolaComboBoxOggetti(cmbUrgenza, $"{ApiConfig.BaseUrl}/api/tickets/urgenze");
                await PopolaComboBoxStringhe(cmbSede, $"{ApiConfig.BaseUrl}/api/tickets/sedi");
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

            // Cambiato in PickMultipleFilesAsync
            var files = await filePicker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    if (!_selectedScreenshots.Any(f => f.Path == file.Path))
                        _selectedScreenshots.Add(file);
                }
                lblFileScelto.Text = $"{_selectedScreenshots.Count} file selezionati";
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
            content.Add(new StringContent(asbPerContoDi.Text ?? ""), "PerContoDi");

            if (_selectedScreenshots.Any())
            {
                foreach (var file in _selectedScreenshots)
                {
                    var fileStream = await file.OpenStreamForReadAsync();
                    var streamContent = new StreamContent(fileStream);
                    // Il backend dovrà essere pronto a ricevere una lista di file con la stessa chiave "Screenshots"
                    content.Add(streamContent, "Screenshots", file.Name);
                }
            }
            try
            {
                btnInvia.IsEnabled = false;
                var response = await _apiClient.PostAsync($"{ApiConfig.BaseUrl}/api/tickets", content);

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
            _selectedScreenshots.Clear();
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

        public void PulisciAllegati()
        {
            _selectedScreenshots.Clear();
            lblFileScelto.Text = "";
            if (btnClearFiles != null) btnClearFiles.Visibility = Visibility.Collapsed;
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

        private void asbPerContoDi_LostFocus(object sender, RoutedEventArgs e)
        {
            var box = sender as AutoSuggestBox;
            if (box == null) return;

            string testoInserito = box.Text.Trim();

            // 1. Se il campo è vuoto, va bene (è opzionale)
            if (string.IsNullOrWhiteSpace(testoInserito))
            {
                return;
            }

            // 2. Controlla se il testo corrisponde esattamente (case-insensitive) a un utente nella lista
            // _allAdUsers deve essere popolata. Se è vuota, consideriamo tutto non valido.
            var utenteValido = _allAdUsers.FirstOrDefault(u => u.Equals(testoInserito, StringComparison.OrdinalIgnoreCase));

            if (utenteValido != null)
            {
                // Se esiste ma il casing è diverso (es. "mario rossi" vs "Mario Rossi"), 
                // lo correggiamo con quello della lista per pulizia
                box.Text = utenteValido;
            }
            else
            {
                // 3. Se NON esiste, cancelliamo il testo per obbligare una selezione valida
                box.Text = string.Empty;

                // Opzionale: Se vuoi avvisare l'utente che ha sbagliato, puoi usare un TeachingTip o un piccolo dialogo,
                // ma spesso cancellare il testo è il feedback standard per "input non valido".
            }
        }
    }
}