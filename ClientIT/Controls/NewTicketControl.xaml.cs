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

        public NewTicketControl()
        {
            this.InitializeComponent();
            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (s, c, ch, e) => true };
            _apiClient = new HttpClient(handler);
        }

        // Metodo per popolare le tendine usando le liste già caricate in MainWindow
        public void SetupData(IList<Tipologia> tipologie, IList<Urgenza> urgenze, IList<string> sedi)
        {
            CmbTipologia.ItemsSource = tipologie;
            CmbUrgenza.ItemsSource = urgenze;
            CmbSede.ItemsSource = sedi;

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
                if (!string.IsNullOrWhiteSpace(TxtPerContoDi.Text))
                {
                    content.Add(new StringContent(TxtPerContoDi.Text), "PerContoDi"); // Aggiungi se l'API è stata aggiornata, o accoda al messaggio
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
            TxtPerContoDi.Text = "";
            if (CmbTipologia.Items.Count > 0) CmbTipologia.SelectedIndex = 0;
        }

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}