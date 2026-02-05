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
using TicketManager;

namespace ClientIT
{
    public sealed partial class MainWindow : Window
    {
        private HttpClient _apiClient;
        // ⚠️ Assicurati che la porta corrisponda a quella del tuo progetto API

        // --- LISTE PER I COMBOBOX E FILTRI ---
        public ObservableCollection<Stato> AllStati { get; } = new();
        public ObservableCollection<ItUtente> AllItUsers { get; } = new();
        public ObservableCollection<Tipologia> AllTipologie { get; } = new();
        public ObservableCollection<Urgenza> AllUrgenze { get; } = new();
        private List<string> _allAdUsers = new();
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

            NewTicketControlElement.TicketCreated += async (s, args) =>
            {
                await ShowTicketListAndRefresh();
            };

            ProjectDetailControlElement.BackRequested += (s, args) => ShowProjectsButton_Click(null, null);
        }

        private bool _isFirstActivation = true;

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (_isFirstActivation)
            {
                _isFirstActivation = false;
               // _ = LoadDataAsync();
            }
        }

        private async Task LoadProjectsAsync()
        {
            try
            {
                var projects = await _apiClient.GetFromJsonAsync<List<ProjectViewModel>>($"{ApiConfig.BaseUrl}/api/progetti/all");
                ProjectListView.ItemsSource = projects;
            }
            catch { }
        }

        private async void ShowProjectsButton_Click(object sender, RoutedEventArgs e)
        {
            // Nascondi tutto il resto
            ListViewArea.Visibility = Visibility.Collapsed; // Ticket List
            DetailViewArea.Visibility = Visibility.Collapsed;
            StatisticsViewArea.Visibility = Visibility.Collapsed;
            NewTicketViewArea.Visibility = Visibility.Collapsed;
            NewProjectViewArea.Visibility = Visibility.Collapsed;
            UserAdminViewArea.Visibility = Visibility.Collapsed;
            ProjectDetailViewArea.Visibility = Visibility.Collapsed;

            // Mostra Lista Progetti
            ProjectListViewArea.Visibility = Visibility.Visible;

            await LoadProjectsAsync();
        }
        private void ProjectListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ProjectViewModel project)
            {
                ProjectListViewArea.Visibility = Visibility.Collapsed;
                ProjectDetailViewArea.Visibility = Visibility.Visible;

                //var currentUser = UserListView.SelectedItem as ItUtente ?? new ItUtente { Nome = "Me" };
                var currentUser = new ItUtente
                {
                    Id = App.CurrentUser.Id,
                    UsernameAd = App.CurrentUser.UsernameAd,
                    Nome = App.CurrentUser.UsernameAd, // O NomeCompleto se lo hai nell'AuthData
                    Permesso = App.CurrentUser.Permesso
                };
                // CHIAMATA AGGIORNATA: Passa anche Stati e Utenti
                ProjectDetailControlElement.Load(project, currentUser, AllStati.ToList(), AllItUsers.ToList());
            }
        }
        private async void ShowDocsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebarButtons(sender as Button);

            if (UserListView != null) UserListView.SelectedIndex = -1;

            // Nascondi tutto
            ListViewArea.Visibility = Visibility.Collapsed;
            DetailViewArea.Visibility = Visibility.Collapsed;
            StatisticsViewArea.Visibility = Visibility.Collapsed;
            NewTicketViewArea.Visibility = Visibility.Collapsed;
            NewProjectViewArea.Visibility = Visibility.Collapsed;
            UserAdminViewArea.Visibility = Visibility.Collapsed;
            if (ProjectListViewArea != null) ProjectListViewArea.Visibility = Visibility.Collapsed;
            if (ProjectDetailViewArea != null) ProjectDetailViewArea.Visibility = Visibility.Collapsed;

            // Mostra Documentazione
            DocumentationViewArea.Visibility = Visibility.Visible;

            // Carica i dati passando le tipologie (per la combobox filtri)
            await DocumentationControlElement.LoadData(AllTipologie.ToList());
        }
        private void UpdateSidebarButtons(Button selectedButton)
        {
            // Resetta lo stile di tutti i bottoni
            if (ShowAllButton != null) ShowAllButton.Style = null;
            if (ShowProjectsButton != null) ShowProjectsButton.Style = null;
            if (UserAdminButton != null) UserAdminButton.Style = null;
            if (NewProjectButton != null) NewProjectButton.Style = null;
            if (NewTicketButton != null) NewTicketButton.Style = null;

            // IMPORTANTE: Includi il nuovo bottone
            if (ShowDocsButton != null) ShowDocsButton.Style = null;

            // Applica lo stile Accent a quello selezionato
            if (selectedButton != null)
            {
                selectedButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
        }
        private void NewTicketButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserListView != null) UserListView.SelectedIndex = -1;

            // NASCONDI TUTTO
            ListViewArea.Visibility = Visibility.Collapsed;
            DetailViewArea.Visibility = Visibility.Collapsed;
            StatisticsViewArea.Visibility = Visibility.Collapsed;
            UserAdminViewArea.Visibility = Visibility.Collapsed;
            NewProjectViewArea.Visibility = Visibility.Collapsed;

            // Nascondi aree progetti (NUOVO)
            if (ProjectListViewArea != null) ProjectListViewArea.Visibility = Visibility.Collapsed;
            if (ProjectDetailViewArea != null) ProjectDetailViewArea.Visibility = Visibility.Collapsed;

            // MOSTRA NUOVO TICKET
            NewTicketViewArea.Visibility = Visibility.Visible;

            NewTicketControlElement.SetupData(AllTipologie, AllUrgenze, AllSedi, _allAdUsers);
        }
        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserListView != null) UserListView.SelectedIndex = -1;

            ListViewArea.Visibility = Visibility.Collapsed;
            DetailViewArea.Visibility = Visibility.Collapsed;
            StatisticsViewArea.Visibility = Visibility.Collapsed;
            NewTicketViewArea.Visibility = Visibility.Collapsed;
            UserAdminViewArea.Visibility = Visibility.Collapsed;

            // Nascondi aree progetti (NUOVO)
            if (ProjectListViewArea != null) ProjectListViewArea.Visibility = Visibility.Collapsed;
            if (ProjectDetailViewArea != null) ProjectDetailViewArea.Visibility = Visibility.Collapsed;

            // MOSTRA CREA PROGETTO
            NewProjectViewArea.Visibility = Visibility.Visible;

            NewProjectControlElement.SetupReferenceData(AllItUsers.ToList(), AllStati.ToList());
        }
        // --- NUOVO: GESTIONE BOTTONE SBLOCCA UTENTE ---
        private void UserAdminButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserListView != null) UserListView.SelectedIndex = -1;

            ListViewArea.Visibility = Visibility.Collapsed;
            DetailViewArea.Visibility = Visibility.Collapsed;
            StatisticsViewArea.Visibility = Visibility.Collapsed;
            NewTicketViewArea.Visibility = Visibility.Collapsed;
            NewProjectViewArea.Visibility = Visibility.Collapsed;

            // Nascondi aree progetti (NUOVO)
            if (ProjectListViewArea != null) ProjectListViewArea.Visibility = Visibility.Collapsed;
            if (ProjectDetailViewArea != null) ProjectDetailViewArea.Visibility = Visibility.Collapsed;

            // MOSTRA ADMIN
            UserAdminViewArea.Visibility = Visibility.Visible;
        }

        private async Task ShowTicketListAndRefresh()
        {
            NewTicketViewArea.Visibility = Visibility.Collapsed;
            UserAdminViewArea.Visibility = Visibility.Collapsed; // <--- Nascondi Admin
            StatisticsViewArea.Visibility = Visibility.Collapsed;
            ListViewArea.Visibility = Visibility.Visible;
            NewProjectViewArea.Visibility = Visibility.Collapsed; // <--- AGGIUNGI
            await LoadTicketsAsync();
        }

        // --- CARICAMENTO DATI ---

        public async Task LoadDataAsync()
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
                var stati = await _apiClient.GetFromJsonAsync<List<Stato>>($"{ApiConfig.BaseUrl}/api/tickets/stati", options);
                AllStati.Clear();
                if (stati != null) foreach (var s in stati) AllStati.Add(s);
            }
            catch { }

            // 2. Utenti IT
            try
            {
                var utenti = await _apiClient.GetFromJsonAsync<List<ItUtente>>($"{ApiConfig.BaseUrl}/api/auth/users", options);
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
                var tipologie = await _apiClient.GetFromJsonAsync<List<Tipologia>>($"{ApiConfig.BaseUrl}/api/tickets/tipologie", options);
                AllTipologie.Clear();
                if (tipologie != null) foreach (var t in tipologie) AllTipologie.Add(t);
            }
            catch { }

            // 4. Urgenze
            try
            {
                var urgenze = await _apiClient.GetFromJsonAsync<List<Urgenza>>($"{ApiConfig.BaseUrl}/api/tickets/urgenze", options);
                AllUrgenze.Clear();
                if (urgenze != null) foreach (var u in urgenze) AllUrgenze.Add(u);
            }
            catch { }

            // 5. Sedi (NUOVO)
            try
            {
                var sedi = await _apiClient.GetFromJsonAsync<List<string>>($"{ApiConfig.BaseUrl}/api/tickets/sedi", options);
                AllSedi.Clear();
                if (sedi != null) foreach (var s in sedi) AllSedi.Add(s);
            }
            catch { }

            // 6. Utenti AD
            try
            {
                var response = await _apiClient.GetAsync($"{ApiConfig.BaseUrl}/api/auth/ad-users-list");
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var users = JsonSerializer.Deserialize<List<string>>(json, options);
                    if (users != null) _allAdUsers = users;
                }
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

                if (FilterNticket != null && !string.IsNullOrWhiteSpace(FilterNticket.Text))
                {
                    if (int.TryParse(FilterNticket.Text, out int nTicketVal))
                    {
                        queryParams.Add($"nticket={nTicketVal}");
                    }
                }

                // Costruiamo l'URL finale
                string url = $"{ApiConfig.BaseUrl}/api/tickets/all";
                if (queryParams.Any())
                {
                    url += "?" + string.Join("&", queryParams);
                }
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                // Chiamata API
                var tickets = await _apiClient.GetFromJsonAsync<List<TicketViewModel>>(url, options);
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
            if (UserListView != null) UserListView.SelectedIndex = -1;
            ResetFiltersVisuals();

            // NASCONDI TUTTO IL RESTO
            NewTicketViewArea.Visibility = Visibility.Collapsed;
            DetailViewArea.Visibility = Visibility.Collapsed;
            StatisticsViewArea.Visibility = Visibility.Collapsed;
            UserAdminViewArea.Visibility = Visibility.Collapsed;
            NewProjectViewArea.Visibility = Visibility.Collapsed;
            if (ProjectListViewArea != null) ProjectListViewArea.Visibility = Visibility.Collapsed;
            if (ProjectDetailViewArea != null) ProjectDetailViewArea.Visibility = Visibility.Collapsed;

            ListViewArea.Visibility = Visibility.Visible;

            await LoadTicketsAsync();
        }

        // 2. Selezione Utente (Colonna sinistra)
        private async void UserListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UserListView.SelectedIndex != -1)
            {
                if (FilterAssegnato != null) FilterAssegnato.SelectedIndex = -1;

                // Nascondi tutto tranne la lista ticket
                DetailViewArea.Visibility = Visibility.Collapsed;
                StatisticsViewArea.Visibility = Visibility.Collapsed;
                NewTicketViewArea.Visibility = Visibility.Collapsed;
                UserAdminViewArea.Visibility = Visibility.Collapsed;
                NewProjectViewArea.Visibility = Visibility.Collapsed;

                if (ProjectListViewArea != null) ProjectListViewArea.Visibility = Visibility.Collapsed;
                if (ProjectDetailViewArea != null) ProjectDetailViewArea.Visibility = Visibility.Collapsed;

                ListViewArea.Visibility = Visibility.Visible;

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
            if (FilterNticket != null) FilterNticket.Text = "";
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

                // 3. Switch della vista
                ListViewArea.Visibility = Visibility.Collapsed;
                DetailViewArea.Visibility = Visibility.Visible;
                // Nascondiamo gli altri
                StatisticsViewArea.Visibility = Visibility.Collapsed;
                NewTicketViewArea.Visibility = Visibility.Collapsed;
                UserAdminViewArea.Visibility = Visibility.Collapsed;
            }
        }

        // Quando clicco "Torna alla lista"
        private async void BackToList_Click(object sender, RoutedEventArgs e)
        {
            DetailControl.ViewModel = null;
            TicketListView.SelectedItem = null;

            ListViewArea.Visibility = Visibility.Visible;
            DetailViewArea.Visibility = Visibility.Collapsed;
            StatisticsViewArea.Visibility = Visibility.Collapsed;
            NewTicketViewArea.Visibility = Visibility.Collapsed;
            UserAdminViewArea.Visibility = Visibility.Collapsed; // <--- Nascondi Admin
            NewProjectViewArea.Visibility = Visibility.Collapsed; // <--- AGGIUNGI
            await LoadTicketsAsync();
        }
        private async void StatsButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserListView != null) UserListView.SelectedIndex = -1;

            ListViewArea.Visibility = Visibility.Collapsed;
            DetailViewArea.Visibility = Visibility.Collapsed;
            NewTicketViewArea.Visibility = Visibility.Collapsed;
            UserAdminViewArea.Visibility = Visibility.Collapsed;
            NewProjectViewArea.Visibility = Visibility.Collapsed;

            // Nascondi aree progetti (NUOVO)
            if (ProjectListViewArea != null) ProjectListViewArea.Visibility = Visibility.Collapsed;
            if (ProjectDetailViewArea != null) ProjectDetailViewArea.Visibility = Visibility.Collapsed;

            // MOSTRA STATISTICHE
            StatisticsViewArea.Visibility = Visibility.Visible;

            await StatsControl.LoadStats();
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

        // --- REFRESH ---
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Ricarica solo i ticket
            await LoadTicketsAsync();
        }

        private async Task SaveFullTicketStateAsync(int nticket)
        {
            var tickets = TicketListView?.ItemsSource as List<TicketViewModel>;
            var ticket = tickets?.FirstOrDefault(t => t.Nticket == nticket);

            if (ticket == null) return;

            if (RootGrid != null) RootGrid.IsHitTestVisible = false;

            try
            {
                string url = $"{ApiConfig.BaseUrl}/api/tickets/{nticket}/update";

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