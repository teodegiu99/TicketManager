using ClientIT.Controls;
using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClientIT
{
    public sealed partial class MainWindow : Window
    {
        private HttpClient _apiClient;
        // ⚠️ Assicurati che la porta corrisponda a quella del tuo progetto API
        private string _apiBaseUrl = "http://localhost:5210";

        // --- LISTE PER I COMBOBOX E FILTRI ---
        public ObservableCollection<Stato> AllStati { get; } = new();
        public ObservableCollection<ItUtente> AllItUsers { get; } = new();
        public ObservableCollection<Tipologia> AllTipologie { get; } = new();
        public ObservableCollection<Urgenza> AllUrgenze { get; } = new();

        // NUOVA LISTA: SEDI
        public ObservableCollection<string> AllSedi { get; } = new();

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Gestione Ticket (IT)";

            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _apiClient = new HttpClient(handler);

            this.Activated += MainWindow_Activated;
        }

        private bool _isFirstActivation = true;

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (_isFirstActivation)
            {
                _isFirstActivation = false;
                _ = LoadDataAsync();
            }
        }

        // --- CARICAMENTO DATI ---

        private async Task LoadDataAsync()
        {
            if (LoadingProgressRing != null)
            {
                LoadingProgressRing.IsActive = true;
                LoadingProgressRing.Visibility = Visibility.Visible;
            }
            if (RootGrid != null) RootGrid.IsHitTestVisible = false;

            try
            {
                // Carica tutte le liste di riferimento (Stati, Utenti, Sedi, ecc.)
                await LoadReferenceDataAsync();

                // Carica i ticket iniziali (senza filtri)
                await LoadTicketsAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Errore nel caricamento dati: {ex.Message}");
            }
            finally
            {
                if (LoadingProgressRing != null)
                {
                    LoadingProgressRing.IsActive = false;
                    LoadingProgressRing.Visibility = Visibility.Collapsed;
                }
                if (RootGrid != null) RootGrid.IsHitTestVisible = true;
            }
        }

        private async Task LoadReferenceDataAsync()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // 1. Stati
            try
            {
                var stati = await _apiClient.GetFromJsonAsync<List<Stato>>($"{_apiBaseUrl}/api/tickets/stati", options);
                AllStati.Clear();
                if (stati != null) foreach (var s in stati) AllStati.Add(s);
            }
            catch { }

            // 2. Utenti IT
            try
            {
                var utenti = await _apiClient.GetFromJsonAsync<List<ItUtente>>($"{_apiBaseUrl}/api/auth/users", options);
                AllItUsers.Clear();
                var nonAssegnato = ItUtente.NonAssegnato ?? new ItUtente { Id = 0, UsernameAd = "Non assegnato" };
                AllItUsers.Add(nonAssegnato);

                if (utenti != null)
                {
                    // Popola la lista a sinistra e quella nei filtri
                    if (UserListView != null) UserListView.ItemsSource = utenti;
                    foreach (var u in utenti) AllItUsers.Add(u);
                }
            }
            catch { }

            // 3. Tipologie
            try
            {
                var tipologie = await _apiClient.GetFromJsonAsync<List<Tipologia>>($"{_apiBaseUrl}/api/tickets/tipologie", options);
                AllTipologie.Clear();
                if (tipologie != null) foreach (var t in tipologie) AllTipologie.Add(t);
            }
            catch { }

            // 4. Urgenze
            try
            {
                var urgenze = await _apiClient.GetFromJsonAsync<List<Urgenza>>($"{_apiBaseUrl}/api/tickets/urgenze", options);
                AllUrgenze.Clear();
                if (urgenze != null) foreach (var u in urgenze) AllUrgenze.Add(u);
            }
            catch { }

            // 5. Sedi (NUOVO)
            try
            {
                var sedi = await _apiClient.GetFromJsonAsync<List<string>>($"{_apiBaseUrl}/api/tickets/sedi", options);
                AllSedi.Clear();
                if (sedi != null) foreach (var s in sedi) AllSedi.Add(s);
            }
            catch { }
        }

        // --- CARICAMENTO TICKET (NUOVO SISTEMA DI FILTRAGGIO) ---

        private async Task LoadTicketsAsync()
        {
            if (TicketListView != null) TicketListView.ItemsSource = null;

            try
            {
                // Costruiamo la lista dei parametri query
                var queryParams = new List<string>();

                // 1. Ricerca Testuale
                if (SearchBox != null && !string.IsNullOrWhiteSpace(SearchBox.Text))
                {
                    queryParams.Add($"search={Uri.EscapeDataString(SearchBox.Text)}");
                }

                // 2. Controllo conflitti: Se è selezionato un utente a sinistra, ha la priorità sul filtro "Assegnato" del flyout
                int? assegnatoId = null;

                if (UserListView != null && UserListView.SelectedItem is ItUtente selectedUser)
                {
                    assegnatoId = selectedUser.Id;
                }
                else if (FilterAssegnato != null && FilterAssegnato.SelectedValue is int flyoutUserId && flyoutUserId > 0)
                {
                    assegnatoId = flyoutUserId;
                }

                if (assegnatoId.HasValue)
                {
                    queryParams.Add($"assegnatoa_id={assegnatoId.Value}");
                }

                // 3. Altri Filtri dal Flyout
                if (FilterStato?.SelectedValue is int sId)
                    queryParams.Add($"stato_id={sId}");

                if (FilterTipologia?.SelectedValue is int tId)
                    queryParams.Add($"tipologia_id={tId}");

                if (FilterUrgenza?.SelectedValue is int uId)
                    queryParams.Add($"urgenza_id={uId}");

                if (FilterSede?.SelectedItem is string sede && !string.IsNullOrEmpty(sede))
                    queryParams.Add($"sede={Uri.EscapeDataString(sede)}");

                if (FilterMacchina != null && !string.IsNullOrWhiteSpace(FilterMacchina.Text))
                    queryParams.Add($"macchina={Uri.EscapeDataString(FilterMacchina.Text)}");

                if (FilterUsername != null && !string.IsNullOrWhiteSpace(FilterUsername.Text))
                    queryParams.Add($"username={Uri.EscapeDataString(FilterUsername.Text)}");

                // Costruiamo l'URL finale
                string url = $"{_apiBaseUrl}/api/tickets/all";
                if (queryParams.Any())
                {
                    url += "?" + string.Join("&", queryParams);
                }

                // Chiamata API
                var tickets = await _apiClient.GetFromJsonAsync<List<TicketViewModel>>(url);
                if (TicketListView != null) TicketListView.ItemsSource = tickets;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Impossibile caricare i ticket: {ex.Message}");
            }
        }


        // --- GESTORI EVENTI UI (FILTRI & NAVBAR) ---

        // 1. Click su "Tutti i Ticket" (Colonna sinistra)
        private async void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Resetta selezione sinistra
            if (UserListView != null) UserListView.SelectedIndex = -1;

            // Resetta filtri visuali (Opzionale: se vuoi resettare anche la ricerca quando premi qui)
            ResetFiltersVisuals();

            await LoadTicketsAsync();
        }

        // 2. Selezione Utente (Colonna sinistra)
        private async void UserListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Se stiamo deselezionando (SelectedIndex diventa -1), non facciamo nulla qui,
            // ci penseranno gli altri metodi.
            if (UserListView.SelectedIndex != -1)
            {
                // Se seleziono a sinistra, resetto il combo nel flyout per evitare confusione visiva
                if (FilterAssegnato != null) FilterAssegnato.SelectedIndex = -1;

                await LoadTicketsAsync();
            }
        }

        // 3. Ricerca (Invio nella SearchBar)
        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            // Resetta selezione utente laterale per cercare globalmente
            if (UserListView != null) UserListView.SelectedIndex = -1;

            await LoadTicketsAsync();
        }

        // 4. Click su "Applica" nel Flyout
        private async void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            // Chiude il Flyout (il pulsante "Filtri" è il genitore del Flyout)
            // In WinUI 3 l'accesso al Flyout parent è meno diretto, ma il click nasconde il flyout di default
            // se non impostato diversamente. Se rimane aperto, useremo:
            if (sender is Button btn && btn.XamlRoot != null)
            {
                // Codice per chiudere flyout se necessario, solitamente automatico su Button Click
            }

            // Resetta selezione sinistra per dare priorità ai filtri avanzati
            if (UserListView != null) UserListView.SelectedIndex = -1;

            await LoadTicketsAsync();
        }

        // 5. Click su "Resetta" nel Flyout
        private async void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            ResetFiltersVisuals();
            if (UserListView != null) UserListView.SelectedIndex = -1;

            await LoadTicketsAsync();
        }

        private void ResetFiltersVisuals()
        {
            if (SearchBox != null) SearchBox.Text = "";
            if (FilterStato != null) FilterStato.SelectedIndex = -1;
            if (FilterAssegnato != null) FilterAssegnato.SelectedIndex = -1;
            if (FilterTipologia != null) FilterTipologia.SelectedIndex = -1;
            if (FilterUrgenza != null) FilterUrgenza.SelectedIndex = -1;
            if (FilterSede != null) FilterSede.SelectedIndex = -1;
            if (FilterMacchina != null) FilterMacchina.Text = "";
            if (FilterUsername != null) FilterUsername.Text = "";
        }


        // --- GESTIONE MODIFICA TICKET (INVARIATA) ---

        // Apertura Modale Dettaglio al Click
        private void TicketListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TicketViewModel ticket)
            {
                // 1. Passa i dati al controllo di dettaglio (che è già nello XAML)
                DetailControl.ViewModel = ticket;

                // 2. Passa le liste per le dropdown
                DetailControl.StatoOptions = AllStati;
                DetailControl.AssigneeOptions = AllItUsers;
                DetailControl.TipologiaOptions = AllTipologie;
                DetailControl.UrgenzaOptions = AllUrgenze;

                // Nota: Gli eventi (TicketStateChanged, ecc.) sono già collegati nello XAML 
                // ai metodi OnTicketStateChanged, ecc., quindi funzioneranno automaticamente.

                // 3. Switch della vista
                ListViewArea.Visibility = Visibility.Collapsed;
                DetailViewArea.Visibility = Visibility.Visible;
            }
        }

        // Quando clicco "Torna alla lista"
        // Quando clicco "Torna alla lista"
        private async void BackToList_Click(object sender, RoutedEventArgs e)
        {
            // 1. Pulisci il dettaglio
            DetailControl.ViewModel = null;

            // 2. Deseleziona l'item
            TicketListView.SelectedItem = null;

            // 3. Switch della vista
            DetailViewArea.Visibility = Visibility.Collapsed;
            ListViewArea.Visibility = Visibility.Visible;

            // 4. CRUCIALE: Ricarica i ticket per aggiornare lo stato (es. rimuovere se chiuso)
            //    Mantiene il filtro utente corrente se c'è.
            await LoadTicketsAsync();
        }

        public async void OnTicketStateChanged(object sender, TicketStateChangedEventArgs e)
        {
            await SaveFullTicketStateAsync(e.Nticket);
        }

        public async void OnTicketAssigneeChanged(object sender, TicketAssigneeChangedEventArgs e)
        {
            await SaveFullTicketStateAsync(e.Nticket);
        }

        public async void OnTicketPropertyChanged(object sender, TicketGenericChangedEventArgs e)
        {
            await SaveFullTicketStateAsync(e.Nticket);
        }

        private async Task SaveFullTicketStateAsync(int nticket)
        {
            // Trova il ViewModel aggiornato nella lista
            var tickets = TicketListView?.ItemsSource as List<TicketViewModel>;
            var ticket = tickets?.FirstOrDefault(t => t.Nticket == nticket);

            if (ticket == null) return;

            if (RootGrid != null) RootGrid.IsHitTestVisible = false;
            // Evitiamo di mostrare il loading pieno per salvataggi veloci, blocchiamo solo input

            try
            {
                string url = $"{_apiBaseUrl}/api/tickets/{nticket}/update";

                var request = new
                {
                    StatoId = ticket.StatoId,
                    AssegnatoaId = ticket.AssegnatoaId == 0 ? null : ticket.AssegnatoaId,
                    UrgenzaId = ticket.UrgenzaId,
                    TipologiaId = ticket.TipologiaId,
                    Note = ticket.Note
                };

                var response = await _apiClient.PutAsJsonAsync(url, request);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Errore salvataggio: {ex.Message}");
                // Ricarica per ripristinare i dati corretti in caso di errore
                await LoadTicketsAsync();
            }
            finally
            {
                if (RootGrid != null) RootGrid.IsHitTestVisible = true;
            }
        }

        private async Task ShowErrorDialog(string content)
        {
            if (RootGrid?.XamlRoot == null) return;
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Errore",
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = RootGrid.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }
}