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

        // --- LISTE PER I COMBOBOX E FILTRI ---
        public ObservableCollection<Stato> AllStati { get; } = new();
        public ObservableCollection<ItUtente> AllItUsers { get; } = new();
        public ObservableCollection<Tipologia> AllTipologie { get; } = new();
        public ObservableCollection<Urgenza> AllUrgenze { get; } = new();
        public ObservableCollection<string> AllSedi { get; } = new();
        private List<string> _allAdUsers = new();

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

            // Eventi dai controlli utente
            NewTicketControlElement.TicketCreated += async (s, args) =>
            {
                await ShowTicketListAndRefresh(); // ORA FUNZIONA PERCHE' RITORNA TASK
            };

            ProjectDetailControlElement.BackRequested += (s, args) => ShowProjectsButton_Click(ShowProjectsButton, null);
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

        // --- HELPER DI NAVIGAZIONE ---

        private void HideAllViews()
        {
            // Ticket
            if (ListViewArea != null) ListViewArea.Visibility = Visibility.Collapsed;
            if (DetailViewArea != null) DetailViewArea.Visibility = Visibility.Collapsed;
            if (NewTicketViewArea != null) NewTicketViewArea.Visibility = Visibility.Collapsed;
            if (StatisticsViewArea != null) StatisticsViewArea.Visibility = Visibility.Collapsed;

            // Progetti
            if (ProjectListViewArea != null) ProjectListViewArea.Visibility = Visibility.Collapsed;
            if (ProjectDetailViewArea != null) ProjectDetailViewArea.Visibility = Visibility.Collapsed;
            if (NewProjectViewArea != null) NewProjectViewArea.Visibility = Visibility.Collapsed;

            // Admin
            if (UserAdminViewArea != null) UserAdminViewArea.Visibility = Visibility.Collapsed;

            // Documentazione
            if (DocumentationViewArea != null) DocumentationViewArea.Visibility = Visibility.Collapsed;
        }

        private void UpdateSidebarButtons(Button selectedButton)
        {
            if (ShowAllButton != null) ShowAllButton.Style = null;
            if (ShowProjectsButton != null) ShowProjectsButton.Style = null;
            if (UserAdminButton != null) UserAdminButton.Style = null;
            if (NewProjectButton != null) NewProjectButton.Style = null;
            if (NewTicketButton != null) NewTicketButton.Style = null;
            if (ShowDocsButton != null) ShowDocsButton.Style = null;
            if (StatsButton != null) StatsButton.Style = null;

            if (selectedButton != null)
            {
                selectedButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
        }

        // --- METODO CORE PER LA NAVIGAZIONE ALLA LISTA TICKET ---
        // Questo metodo contiene la logica condivisa ed è awaitable (Task)
        private async Task GoToAllTicketsViewAsync()
        {
            UpdateSidebarButtons(ShowAllButton); // Evidenzia il bottone "Tutti i Ticket"
            if (UserListView != null) UserListView.SelectedIndex = -1;
            ResetFiltersVisuals();

            HideAllViews();

            if (ListViewArea != null) ListViewArea.Visibility = Visibility.Visible;
            await LoadTicketsAsync();
        }

        // --- GESTORI EVENTI DI NAVIGAZIONE ---

        // 1. Click su "Tutti i Ticket"
        private async void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            await GoToAllTicketsViewAsync();
        }

        // Metodo helper chiamato dopo la creazione di un ticket
        // Restituisce Task invece di void per poter usare 'await'
        private async Task ShowTicketListAndRefresh()
        {
            await GoToAllTicketsViewAsync();
        }

        // 2. Click su "Tutti i Progetti"
        private async void ShowProjectsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebarButtons(sender as Button);
            if (UserListView != null) UserListView.SelectedIndex = -1;

            HideAllViews();

            if (ProjectListViewArea != null) ProjectListViewArea.Visibility = Visibility.Visible;
            await LoadProjectsAsync();
        }

        // 3. Click su "Documentazione"
        private async void ShowDocsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebarButtons(sender as Button);
            if (UserListView != null) UserListView.SelectedIndex = -1;

            HideAllViews();

            if (DocumentationViewArea != null) DocumentationViewArea.Visibility = Visibility.Visible;
            await DocumentationControlElement.LoadData(AllTipologie.ToList());
        }

        // 4. Click su "Nuovo Ticket"
        private void NewTicketButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebarButtons(sender as Button);
            if (UserListView != null) UserListView.SelectedIndex = -1;

            HideAllViews();

            if (NewTicketViewArea != null) NewTicketViewArea.Visibility = Visibility.Visible;
            NewTicketControlElement.SetupData(AllTipologie, AllUrgenze, AllSedi, _allAdUsers);
        }

        // 5. Click su "Crea Progetto"
        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebarButtons(sender as Button);
            if (UserListView != null) UserListView.SelectedIndex = -1;

            HideAllViews();

            if (NewProjectViewArea != null) NewProjectViewArea.Visibility = Visibility.Visible;
            NewProjectControlElement.SetupReferenceData(AllItUsers.ToList(), AllStati.ToList());
        }

        // 6. Click su "Sblocca Utente"
        private void UserAdminButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebarButtons(sender as Button);
            if (UserListView != null) UserListView.SelectedIndex = -1;

            HideAllViews();

            if (UserAdminViewArea != null) UserAdminViewArea.Visibility = Visibility.Visible;
        }

        // 7. Click su "Statistiche"
        private async void StatsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSidebarButtons(sender as Button);
            if (UserListView != null) UserListView.SelectedIndex = -1;

            HideAllViews();

            if (StatisticsViewArea != null) StatisticsViewArea.Visibility = Visibility.Visible;
            await StatsControl.LoadStats();
        }

        // 8. Selezione Utente
        private async void UserListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UserListView.SelectedIndex != -1)
            {
                UpdateSidebarButtons(null);
                if (FilterAssegnato != null) FilterAssegnato.SelectedIndex = -1;

                HideAllViews();

                if (ListViewArea != null) ListViewArea.Visibility = Visibility.Visible;
                await LoadTicketsAsync();
            }
        }


        // --- LOGICA DI CARICAMENTO DATI ---

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
                await LoadReferenceDataAsync();
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

            try
            {
                var stati = await _apiClient.GetFromJsonAsync<List<Stato>>($"{ApiConfig.BaseUrl}/api/tickets/stati", options);
                AllStati.Clear();
                if (stati != null) foreach (var s in stati) AllStati.Add(s);

                var utenti = await _apiClient.GetFromJsonAsync<List<ItUtente>>($"{ApiConfig.BaseUrl}/api/auth/users", options);
                AllItUsers.Clear();
                var nonAssegnato = ItUtente.NonAssegnato ?? new ItUtente { Id = 0, UsernameAd = "Non assegnato" };
                AllItUsers.Add(nonAssegnato);
                if (utenti != null)
                {
                    if (UserListView != null) UserListView.ItemsSource = utenti;
                    foreach (var u in utenti) AllItUsers.Add(u);
                }

                var tipologie = await _apiClient.GetFromJsonAsync<List<Tipologia>>($"{ApiConfig.BaseUrl}/api/tickets/tipologie", options);
                AllTipologie.Clear();
                if (tipologie != null) foreach (var t in tipologie) AllTipologie.Add(t);

                var urgenze = await _apiClient.GetFromJsonAsync<List<Urgenza>>($"{ApiConfig.BaseUrl}/api/tickets/urgenze", options);
                AllUrgenze.Clear();
                if (urgenze != null) foreach (var u in urgenze) AllUrgenze.Add(u);

                var sedi = await _apiClient.GetFromJsonAsync<List<string>>($"{ApiConfig.BaseUrl}/api/tickets/sedi", options);
                AllSedi.Clear();
                if (sedi != null) foreach (var s in sedi) AllSedi.Add(s);

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

        private async Task LoadTicketsAsync()
        {
            if (TicketListView != null) TicketListView.ItemsSource = null;

            try
            {
                var queryParams = new List<string>();

                if (SearchBox != null && !string.IsNullOrWhiteSpace(SearchBox.Text))
                    queryParams.Add($"search={Uri.EscapeDataString(SearchBox.Text)}");

                int? assegnatoId = null;
                if (UserListView != null && UserListView.SelectedItem is ItUtente selectedUser)
                    assegnatoId = selectedUser.Id;
                else if (FilterAssegnato != null && FilterAssegnato.SelectedValue is int flyoutUserId && flyoutUserId > 0)
                    assegnatoId = flyoutUserId;

                if (assegnatoId.HasValue) queryParams.Add($"assegnatoa_id={assegnatoId.Value}");

                if (FilterStato?.SelectedValue is int sId) queryParams.Add($"stato_id={sId}");
                if (FilterTipologia?.SelectedValue is int tId) queryParams.Add($"tipologia_id={tId}");
                if (FilterUrgenza?.SelectedValue is int uId) queryParams.Add($"urgenza_id={uId}");
                if (FilterSede?.SelectedItem is string sede && !string.IsNullOrEmpty(sede)) queryParams.Add($"sede={Uri.EscapeDataString(sede)}");
                if (FilterMacchina != null && !string.IsNullOrWhiteSpace(FilterMacchina.Text)) queryParams.Add($"macchina={Uri.EscapeDataString(FilterMacchina.Text)}");
                if (FilterUsername != null && !string.IsNullOrWhiteSpace(FilterUsername.Text)) queryParams.Add($"username={Uri.EscapeDataString(FilterUsername.Text)}");
                if (FilterNticket != null && !string.IsNullOrWhiteSpace(FilterNticket.Text) && int.TryParse(FilterNticket.Text, out int nTicketVal))
                    queryParams.Add($"nticket={nTicketVal}");

                string url = $"{ApiConfig.BaseUrl}/api/tickets/all";
                if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var tickets = await _apiClient.GetFromJsonAsync<List<TicketViewModel>>(url, options);
                if (TicketListView != null) TicketListView.ItemsSource = tickets;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Impossibile caricare i ticket: {ex.Message}");
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

        // --- ALTRE AZIONI UI ---

        private void ProjectListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ProjectViewModel project)
            {
                HideAllViews();
                ProjectDetailViewArea.Visibility = Visibility.Visible;

                var currentUser = new ItUtente
                {
                    Id = App.CurrentUser.Id,
                    UsernameAd = App.CurrentUser.UsernameAd,
                    Nome = App.CurrentUser.UsernameAd,
                    Permesso = App.CurrentUser.Permesso
                };
                ProjectDetailControlElement.Load(project, currentUser, AllStati.ToList(), AllItUsers.ToList());
            }
        }

        private void TicketListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TicketViewModel ticket)
            {
                DetailControl.ViewModel = ticket;
                DetailControl.StatoOptions = AllStati;
                DetailControl.AssigneeOptions = AllItUsers;
                DetailControl.TipologiaOptions = AllTipologie;
                DetailControl.UrgenzaOptions = AllUrgenze;

                HideAllViews();
                DetailViewArea.Visibility = Visibility.Visible;
            }
        }

        private async void BackToList_Click(object sender, RoutedEventArgs e)
        {
            DetailControl.ViewModel = null;
            if (TicketListView != null) TicketListView.SelectedItem = null;

            HideAllViews();
            ListViewArea.Visibility = Visibility.Visible;

            await LoadTicketsAsync();
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (UserListView != null) UserListView.SelectedIndex = -1;
            await LoadTicketsAsync();
        }

        private async void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            if (UserListView != null) UserListView.SelectedIndex = -1;
            await LoadTicketsAsync();
        }

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

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadTicketsAsync();
        }

        // --- GESTIONE AGGIORNAMENTI TICKET ---

        public async void OnTicketStateChanged(object sender, TicketStateChangedEventArgs e) => await SaveFullTicketStateAsync(e.Nticket);
        public async void OnTicketAssigneeChanged(object sender, TicketAssigneeChangedEventArgs e) => await SaveFullTicketStateAsync(e.Nticket);
        public async void OnTicketPropertyChanged(object sender, TicketGenericChangedEventArgs e) => await SaveFullTicketStateAsync(e.Nticket);

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