using ClientIT.Models; // Assumendo che Tipologia sia qui
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TicketManager; // Per ApiConfig
// Aggiungi using per il DTO della documentazione

namespace ClientIT.Controls
{
    public sealed partial class DocumentationControl : UserControl
    {
        private HttpClient _apiClient;
        private List<DocumentazioneDto> _allDocsCache = new(); // Cache locale di tutto

        // Collezione per i badge delle keyword di filtro
        public ObservableCollection<string> FilterKeywords { get; } = new();

        public DocumentationControl()
        {
            this.InitializeComponent();

            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true };
            _apiClient = new HttpClient(handler);
        }

        // Metodo chiamato dalla MainWindow quando si apre la pagina
        public async Task LoadData(List<Tipologia> tipologie)
        {
            // Popola Tipologie
            FilterTipologia.ItemsSource = tipologie;

            // Scarica tutta la documentazione (o filtra lato server se preferisci)
            // Per ora scarichiamo tutto e filtriamo in memoria (Client-Side Filtering)
            // dato che la documentazione non sarà gigantesca.
            await RefreshDocs();
        }

        private async Task RefreshDocs()
        {
            try
            {
                var docs = await _apiClient.GetFromJsonAsync<List<DocumentazioneDto>>($"{ApiConfig.BaseUrl}/api/documentazione");
                if (docs != null)
                {
                    _allDocsCache = docs;
                    ApplyFilters(); // Mostra i risultati
                }
            }
            catch (Exception ex)
            {
                // Gestione errore silenziosa o dialog
            }
        }

        private void ApplyFilters()
        {
            var query = _allDocsCache.AsEnumerable();

            // 1. SearchBar (Titolo o Soluzione)
            if (!string.IsNullOrWhiteSpace(MainSearchBox.Text))
            {
                string txt = MainSearchBox.Text.ToLower();
                query = query.Where(d => d.Titolo.ToLower().Contains(txt) || d.Soluzione.ToLower().Contains(txt));
            }

            // 2. Tipologia
            if (FilterTipologia.SelectedValue is int tipId)
            {
                query = query.Where(d => d.CategoriaId == tipId);
            }

            // 3. N Ticket
            if (!string.IsNullOrWhiteSpace(FilterNticket.Text) && int.TryParse(FilterNticket.Text, out int nTicket))
            {
                query = query.Where(d => d.Nticket == nTicket);
            }

            // 4. Query SQL
            if (!string.IsNullOrWhiteSpace(FilterQuery.Text))
            {
                string qTxt = FilterQuery.Text.ToLower();
                query = query.Where(d => d.Query != null && d.Query.ToLower().Contains(qTxt));
            }

            // 5. Keywords (Badge)
            // Logica: Il documento deve contenere TUTTE le keyword specificate nel filtro (AND)
            if (FilterKeywords.Any())
            {
                foreach (var filterKey in FilterKeywords)
                {
                    string kLower = filterKey.ToLower();
                    // Controlla se nella lista dei nomi delle keyword del doc c'è quella cercata
                    query = query.Where(d => d.KeywordNomi != null &&
                                             d.KeywordNomi.Any(kn => kn.ToLower().Contains(kLower)));
                }
            }

            // Aggiorna UI
            DocsListView.ItemsSource = query.ToList();
        }

        // --- GESTIONE KEYWORD BADGES ---
        private void TxtFilterKeyword_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AddFilterKeyword();
                e.Handled = true;
            }
        }

        private void BtnAddFilterKeyword_Click(object sender, RoutedEventArgs e) => AddFilterKeyword();

        private void AddFilterKeyword()
        {
            string txt = TxtFilterKeyword.Text.Trim();
            if (!string.IsNullOrEmpty(txt) && !FilterKeywords.Contains(txt))
            {
                FilterKeywords.Add(txt);
            }
            TxtFilterKeyword.Text = "";
            TxtFilterKeyword.Focus(FocusState.Programmatic);
        }

        private void BtnRemoveFilterKeyword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string val) FilterKeywords.Remove(val);
        }

        // --- GESTIONE SEARCH/RESET ---
        private void MainSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ApplyFilters();
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            MainSearchBox.Text = "";
            FilterTipologia.SelectedIndex = -1;
            FilterNticket.Text = "";
            FilterQuery.Text = "";
            FilterKeywords.Clear();

            ApplyFilters();
        }
    }
}