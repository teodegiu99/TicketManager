using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ClientIT.Controls
{
    public sealed partial class NewTicketControl : UserControl
    {
        // Evento per notificare la MainWindow che il ticket è stato creato
        public event EventHandler? TicketCreated;

        private HttpClient _apiClient;
        // Assicurati che l'URL sia corretto
        private string _apiBaseUrl = "http://localhost:5210";
        private List<string> _allAdUsers = new();

        public NewTicketControl()
        {
            this.InitializeComponent();
            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (s, c, ch, e) => true };
            _apiClient = new HttpClient(handler);
        }

        // Metodo per popolare le tendine usando le liste già caricate in MainWindow
        public void SetupData(IList<Tipologia> tipologie, IList<Urgenza> urgenze, IList<string> sedi, IList<string> adUsers)
        {
            CmbTipologia.ItemsSource = tipologie;
            CmbUrgenza.ItemsSource = urgenze;
            CmbSede.ItemsSource = sedi;

            // Salviamo la lista utenti per i suggerimenti
            _allAdUsers = adUsers as List<string> ?? adUsers.ToList();

            if (CmbTipologia.Items.Count > 0) CmbTipologia.SelectedIndex = 0;
            if (CmbUrgenza.Items.Count > 0) CmbUrgenza.SelectedIndex = 0;
            if (CmbSede.Items.Count > 0) CmbSede.SelectedIndex = 0;
        }

        private void CmbTipologia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTipologia.SelectedItem is Tipologia t && t.Nome.Contains("protex", StringComparison.OrdinalIgnoreCase))
            {
                TxtFunzione.Visibility = Visibility.Visible;
            }
            else
            {
                TxtFunzione.Visibility = Visibility.Collapsed;
                TxtFunzione.Text = "";
            }
        }
        private void AsbPerContoDi_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var query = sender.Text.ToLower();
                if (string.IsNullOrWhiteSpace(query)) sender.ItemsSource = _allAdUsers;
                else sender.ItemsSource = _allAdUsers.Where(u => u.ToLower().Contains(query)).ToList();
            }
        }

        private void AsbPerContoDi_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem != null) sender.Text = args.SelectedItem.ToString();
        }

        private void AsbPerContoDi_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is AutoSuggestBox box && _allAdUsers != null && _allAdUsers.Any())
            {
                box.ItemsSource = _allAdUsers;
                box.IsSuggestionListOpen = true;
            }
        }

        private void AsbPerContoDi_LostFocus(object sender, RoutedEventArgs e)
        {
            var box = sender as AutoSuggestBox;
            if (box == null) return;

            string testoInserito = box.Text.Trim();

            // 1. Se il campo è vuoto, va bene (è opzionale)
            if (string.IsNullOrWhiteSpace(testoInserito))
            {
                return;
            }

            // 2. Cerchiamo se il testo inserito esiste nella lista (ignorando maiuscole/minuscole)
            // _allAdUsers viene popolata nel metodo SetupData
            var utenteValido = _allAdUsers.FirstOrDefault(u => u.Equals(testoInserito, StringComparison.OrdinalIgnoreCase));

            if (utenteValido != null)
            {
                // Trovato! Sostituiamo il testo con quello "ufficiale" della lista (per correggere il casing es. "mario" -> "Mario")
                box.Text = utenteValido;
            }
            else
            {
                // 3. Non trovato: svuotiamo il campo per impedire l'invio di dati errati
                // (Replica esattamente il comportamento di ClientUser)
                box.Text = string.Empty;

                 ShowError("L'utente specificato 'per conto di' non esiste nella directory.");
            }
        }

        private async void BtnInvia_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(TxtOggetto.Text) || string.IsNullOrWhiteSpace(TxtMessaggio.Text))
            {
                ShowError("Titolo e Messaggio sono obbligatori.");
                return;
            }

            BtnInvia.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Visible;

            try
            {
                var content = new MultipartFormDataContent();

                // Nota: Assicurati che i nomi delle proprietà combacino con TicketRequest nell'API
                var tipologia = CmbTipologia.SelectedItem as Tipologia;
                var urgenza = CmbUrgenza.SelectedItem as Urgenza;
                var sede = CmbSede.SelectedItem as string;

                content.Add(new StringContent(tipologia?.Nome ?? ""), "ProblemType");
                content.Add(new StringContent(urgenza?.Nome ?? ""), "Urgency");
                content.Add(new StringContent(sede ?? ""), "Sede");
                content.Add(new StringContent(TxtFunzione.Text), "Funzione");
                content.Add(new StringContent(System.Environment.MachineName), "Macchina");
                content.Add(new StringContent(TxtOggetto.Text), "Title");
                content.Add(new StringContent(TxtMessaggio.Text), "Message");

                // NOTA: Il campo "Per Conto Di" non è nel form standard ClientUser, 
                // ma se l'API lo supporta, potresti doverlo aggiungere o gestire lato backend.
                // Se non c'è supporto API, scriviamolo nel testo.
                if (!string.IsNullOrWhiteSpace(AsbPerContoDi.Text))
                {
                    content.Add(new StringContent(AsbPerContoDi.Text), "PerContoDi");
                }

                var response = await _apiClient.PostAsync($"{_apiBaseUrl}/api/tickets", content);

                if (response.IsSuccessStatusCode)
                {
                    // Successo!
                    ClearFields();
                    TicketCreated?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    ShowError($"Errore server: {response.StatusCode} - {err}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Errore di connessione: {ex.Message}");
            }
            finally
            {
                BtnInvia.IsEnabled = true;
                LoadingBar.Visibility = Visibility.Collapsed;
            }
        }

        private void ClearFields()
        {
            TxtOggetto.Text = "";
            TxtMessaggio.Text = "";
            TxtFunzione.Text = "";
            AsbPerContoDi.Text = ""; 
            if (CmbTipologia.Items.Count > 0) CmbTipologia.SelectedIndex = 0;
        }

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}