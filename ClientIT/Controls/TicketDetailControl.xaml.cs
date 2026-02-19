using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq; // Necessario per LINQ
using System.Net.Http; // Necessario per API
using System.Net.Http.Json; // Necessario per JSON
using System.Net.Sockets;
using System.Threading.Tasks;
using TicketManager;

namespace ClientIT.Controls
{
    public sealed partial class TicketDetailControl : UserControl
    {
        private readonly HttpClient _apiClient;

        public TicketDetailControl()
        {
            this.InitializeComponent();

            // Inizializza HttpClient per le chiamate API (Documentazione)
            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _apiClient = new HttpClient(handler);
        }

        // =========================================================
        // 1. DEPENDENCY PROPERTIES
        // =========================================================
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(TicketViewModel), typeof(TicketDetailControl),
                new PropertyMetadata(null, OnViewModelChanged));

        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Recupera l'istanza del controllo
            var control = (TicketDetailControl)d;

            // CORREZIONE: Forza l'aggiornamento delle selezioni quando il ViewModel cambia
            control.UpdateSelections();
        }


        // C:\Users\mdegi\Desktop\TicketManager\ClientIT\Controls\TicketDetailControl.xaml.cs

        // Rimuovi il vecchio OpenScreenshot_Click e sostituiscilo con questo:
        private void OpenScreenshot_Click(object sender, RoutedEventArgs e)
        {
            // Recuperiamo l'allegato dal DataContext del bottone cliccato
            if (sender is Button btn && btn.DataContext is TicketAllegato allegato)
            {
                if (!string.IsNullOrEmpty(allegato.Path))
                {
                    try
                    {
                        // CORREZIONE: Usiamo direttamente il path di rete (UNC)
                        // Impostando UseShellExecute = true, Windows aprirà il file con il programma predefinito (es. Foto)
                        var p = new ProcessStartInfo(allegato.Path)
                        {
                            UseShellExecute = true
                        };
                        Process.Start(p);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Errore apertura allegato: {ex.Message}");

                        // Opzionale: Mostra un avviso all'utente se il file non viene trovato
                        // (es. permessi mancanti o file spostato)
                    }
                }
            }
        }
   
        public TicketViewModel ViewModel
        {
            get => (TicketViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        public Visibility HasCC => (ViewModel != null && !string.IsNullOrWhiteSpace(ViewModel.UtentiCC))
    ? Visibility.Visible
    : Visibility.Collapsed;



        // CORREZIONE: Accediamo a ViewModel invece di Ticket
        public Visibility HasPerContoDi => (ViewModel != null && !string.IsNullOrWhiteSpace(ViewModel.PerContoDi))
            ? Visibility.Visible
            : Visibility.Collapsed;
        public static readonly DependencyProperty StatoOptionsProperty =
            DependencyProperty.Register(nameof(StatoOptions), typeof(IList<Stato>), typeof(TicketDetailControl), new PropertyMetadata(null, OnDataChanged));
        public IList<Stato> StatoOptions { get => (IList<Stato>)GetValue(StatoOptionsProperty); set => SetValue(StatoOptionsProperty, value); }

        public static readonly DependencyProperty AssigneeOptionsProperty =
            DependencyProperty.Register(nameof(AssigneeOptions), typeof(IList<ItUtente>), typeof(TicketDetailControl), new PropertyMetadata(null, OnDataChanged));
        public IList<ItUtente> AssigneeOptions { get => (IList<ItUtente>)GetValue(AssigneeOptionsProperty); set => SetValue(AssigneeOptionsProperty, value); }

        public static readonly DependencyProperty TipologiaOptionsProperty =
            DependencyProperty.Register(nameof(TipologiaOptions), typeof(IList<Tipologia>), typeof(TicketDetailControl), new PropertyMetadata(null, OnDataChanged));
        public IList<Tipologia> TipologiaOptions { get => (IList<Tipologia>)GetValue(TipologiaOptionsProperty); set => SetValue(TipologiaOptionsProperty, value); }

        public static readonly DependencyProperty UrgenzaOptionsProperty =
            DependencyProperty.Register(nameof(UrgenzaOptions), typeof(IList<Urgenza>), typeof(TicketDetailControl), new PropertyMetadata(null, OnDataChanged));
        public IList<Urgenza> UrgenzaOptions { get => (IList<Urgenza>)GetValue(UrgenzaOptionsProperty); set => SetValue(UrgenzaOptionsProperty, value); }

        // =========================================================
        // 2. EVENTI
        // =========================================================

        public event EventHandler<TicketStateChangedEventArgs>? TicketStateChanged;
        public event EventHandler<TicketAssigneeChangedEventArgs>? TicketAssigneeChanged;
        public event EventHandler<TicketGenericChangedEventArgs>? TicketPropertyChanged;

        // =========================================================
        // 3. LOGICA DI AGGIORNAMENTO UI
        // =========================================================

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TicketDetailControl c) c.UpdateSelections();
        }

        private void UpdateSelections()
        {
            if (ViewModel == null) return;

            void UpdateCombo(ComboBox combo, System.Collections.IEnumerable items, object? val, SelectionChangedEventHandler h)
            {
                if (combo == null) return;
                if (items != null && (combo.ItemsSource == null || combo.ItemsSource != items)) combo.ItemsSource = items;

                combo.SelectionChanged -= h;
                combo.SelectedValue = val;
                combo.SelectionChanged += h;
            }

            UpdateCombo(TipologiaCombo, TipologiaOptions, ViewModel.TipologiaId, TipologiaComboBox_SelectionChanged);
            UpdateCombo(UrgenzaCombo, UrgenzaOptions, ViewModel.UrgenzaId, UrgenzaComboBox_SelectionChanged);
            UpdateCombo(StatoCombo, StatoOptions, ViewModel.StatoId, StatoComboBox_SelectionChanged);
            UpdateCombo(AssegnatoCombo, AssigneeOptions, ViewModel.AssegnatoaId ?? 0, AssegnatoaComboBox_SelectionChanged);
        }

        // =========================================================
        // 4. GESTORI EVENTI UI
        // =========================================================

        private void TipologiaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not ComboBox cb || cb.SelectedValue is not int val) return;
            if (val != ViewModel.TipologiaId)
            {
                ViewModel.TipologiaId = val;
                TicketPropertyChanged?.Invoke(this, new TicketGenericChangedEventArgs(ViewModel.Nticket, "TipologiaId", val));
            }
        }

        private void UrgenzaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not ComboBox cb || cb.SelectedValue is not int val) return;
            if (val != ViewModel.UrgenzaId)
            {
                ViewModel.UrgenzaId = val;
                TicketPropertyChanged?.Invoke(this, new TicketGenericChangedEventArgs(ViewModel.Nticket, "UrgenzaId", val));
            }
        }

        // --- MODIFICA PRINCIPALE QUI SOTTO ---
        private async void StatoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not ComboBox cb || cb.SelectedValue is not int val) return;

            if (val != ViewModel.StatoId)
            {
                // --- NUOVO CONTROLLO: Impedisci "In carico" (Id 2) se non assegnato ---
                if (val == 2 && (ViewModel.AssegnatoaId == null || ViewModel.AssegnatoaId == 0))
                {
                    // 1. Mostra l'alert all'utente
                    var dialog = new ContentDialog
                    {
                        Title = "Assegnazione richiesta",
                        Content = "Impossibile mettere il ticket 'In carico' senza averlo prima assegnato a un tecnico. Seleziona un tecnico dalla tendina 'Assegna a'.",
                        CloseButtonText = "OK",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();

                    // 2. Ripristina la tendina allo stato precedente senza far scattare un loop infinito
                    cb.SelectionChanged -= StatoComboBox_SelectionChanged;
                    cb.SelectedValue = ViewModel.StatoId;
                    cb.SelectionChanged += StatoComboBox_SelectionChanged;

                    return; // Blocchiamo l'esecuzione qui
                }

                // 1. Aggiorna stato e notifica parent
                ViewModel.StatoId = val;
                TicketStateChanged?.Invoke(this, new TicketStateChangedEventArgs(ViewModel.Nticket, val));

                // 2. Controllo se è "Terminato" per la documentazione
                // Cerchiamo l'oggetto Stato corrispondente nella lista delle opzioni
                var nuovoStato = StatoOptions?.FirstOrDefault(s => s.Id == val);

                if (nuovoStato != null && nuovoStato.Nome.Equals("Terminato", StringComparison.OrdinalIgnoreCase))
                {
                    await AskToCreateDocumentation();
                }
            }
        }

        // Metodo helper separato per la logica documentazione
        private async Task AskToCreateDocumentation()
        {
            // A. Chiedi conferma all'utente
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Ticket Terminato",
                Content = "Vuoi aggiungere la risoluzione di questo ticket alla Documentazione (Knowledge Base)?",
                PrimaryButtonText = "Sì, aggiungi",
                CloseButtonText = "No",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // B. Prepara dati per la modale
                string titoloSuggerito = ViewModel.Titolo;
                string soluzioneSuggerita = ViewModel.Note ?? "";

                // Convertiamo IList in List per passarlo al dialog (se AddDocDialog accetta List)
                var listTipologie = TipologiaOptions != null ? TipologiaOptions.ToList() : new List<Tipologia>();

                // C. Apri la modale AddDocDialog
                AddDocDialog docDialog = new AddDocDialog(listTipologie, soluzioneSuggerita, titoloSuggerito);
                docDialog.XamlRoot = this.XamlRoot;

                var docResult = await docDialog.ShowAsync();

                if (docResult == ContentDialogResult.Primary)
                {
                    // D. Recupera i dati e invia al Server
                    var dati = docDialog.GetResult();

                    var docRequest = new
                    {
                        Nticket = ViewModel.Nticket,
                        Titolo = dati.Titolo,
                        Soluzione = dati.Soluzione,
                        Query = dati.Query,
                        CategoriaId = dati.CategoriaId,
                        Keywords = dati.Keywords // Lista di stringhe, il server gestirà IDs
                    };

                    try
                    {
                        var response = await _apiClient.PostAsJsonAsync($"{ApiConfig.BaseUrl}/api/documentazione", docRequest);
                        if (response.IsSuccessStatusCode)
                        {
                            // Feedback visivo semplice
                            var successDialog = new ContentDialog
                            {
                                Title = "Successo",
                                Content = "Documentazione creata correttamente!",
                                CloseButtonText = "Ok",
                                XamlRoot = this.XamlRoot
                            };
                            await successDialog.ShowAsync();
                        }
                        else
                        {
                            var err = await response.Content.ReadAsStringAsync();
                            var errDialog = new ContentDialog
                            {
                                Title = "Errore Creazione",
                                Content = $"Impossibile salvare: {err}",
                                CloseButtonText = "Chiudi",
                                XamlRoot = this.XamlRoot
                            };
                            await errDialog.ShowAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        var exDialog = new ContentDialog
                        {
                            Title = "Eccezione",
                            Content = ex.Message,
                            CloseButtonText = "Chiudi",
                            XamlRoot = this.XamlRoot
                        };
                        await exDialog.ShowAsync();
                    }
                }
            }
        }

        private void AssegnatoaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not ComboBox cb || cb.SelectedValue is not int val) return;

            int? idVal = val == 0 ? null : val;

            if (val != (ViewModel.AssegnatoaId ?? 0))
            {
                ViewModel.AssegnatoaId = idVal;
                TicketAssigneeChanged?.Invoke(this, new TicketAssigneeChangedEventArgs(ViewModel.Nticket, val));
            }
        }

        private void NoteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                TicketPropertyChanged?.Invoke(this, new TicketGenericChangedEventArgs(ViewModel.Nticket, "Note", 0));
            }
        }

        // =========================================================
        // 5. UTILS E SCREENSHOT
        // =========================================================

        public string FormatDate(DateTime date)
        {
            // Forza il sistema a trattare la data come UTC, poi converti in Locale (Italiano)
            return DateTime.SpecifyKind(date, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        }

        private async void TeamViewer_Click(object sender, RoutedEventArgs e)
        {
            // 1. Controlliamo se il ViewModel (il ticket) è stato caricato
            if (ViewModel == null)
            {
                var dialog = new ContentDialog { Title = "DEBUG", Content = "Errore: Il ViewModel è NULL!", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                await dialog.ShowAsync();
                return;
            }

            string nomeMacchina = ViewModel.Macchina;

            // 2. Controlliamo se la stringa della macchina è vuota
            if (string.IsNullOrWhiteSpace(nomeMacchina))
            {
                var dialog = new ContentDialog { Title = "Errore", Content = "Il campo 'Macchina' è vuoto!", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                await dialog.ShowAsync();
                return;
            }

            string target = "";

            // 3. Chiamata API per ottenere l'ID TeamViewer
            try
            {
                var response = await _apiClient.GetAsync($"{ApiConfig.BaseUrl}/api/tickets/teamviewer/{Uri.EscapeDataString(nomeMacchina)}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<TeamViewerResponse>();
                    target = result?.idtw;
                    //target = "1489849531";
                }
                else
                {
                    var dialog = new ContentDialog { Title = "Non Trovata", Content = $"La macchina '{nomeMacchina}' non è presente nel database di TeamViewer.", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                    await dialog.ShowAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog { Title = "Errore API", Content = $"Impossibile recuperare l'ID: {ex.Message}", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                await dialog.ShowAsync();
                return;
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                var dialog = new ContentDialog { Title = "Errore", Content = "ID TeamViewer recuperato ma risulta vuoto.", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                await dialog.ShowAsync();
                return;
            }

            // 4. Mostriamo a video il target che stiamo per usare
            var debugDialog = new ContentDialog
            {
                Title = "Connessione in corso",
                Content = $"Provo a connettermi al target ID: '{target}' per la macchina '{nomeMacchina}'",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await debugDialog.ShowAsync();

            // 5. Tentativo di connessione
            try
            {
                // Usa --id invece di -i
                string args = $"--id {target}";

                string[] paths = new[]
                {
                    @"C:\Program Files\TeamViewer\TeamViewer.exe",       // 64-bit 
                    @"C:\Program Files (x86)\TeamViewer\TeamViewer.exe"   // 32-bit 
                };

                string pathTrovato = paths.FirstOrDefault(p => System.IO.File.Exists(p));

                if (pathTrovato != null)
                {
                    var p = new ProcessStartInfo
                    {
                        FileName = pathTrovato,
                        Arguments = args,
                        UseShellExecute = true,
                        // FONDAMENTALE: Imposta la cartella di lavoro corretta
                        WorkingDirectory = System.IO.Path.GetDirectoryName(pathTrovato)
                    };
                    Process.Start(p);
                }
                else
                {
                    var p = new ProcessStartInfo
                    {
                        FileName = "teamviewer.exe",
                        Arguments = args,
                        UseShellExecute = true
                    };
                    Process.Start(p);
                }
            }
            catch (Exception ex)
            {
                // 6. Se scoppia un errore durante l'avvio, ce lo mostra
                var errDialog = new ContentDialog
                {
                    Title = "ERRORE AVVIO",
                    Content = $"Impossibile lanciare TeamViewer.\nMotivo: {ex.Message}",
                    CloseButtonText = "Chiudi",
                    XamlRoot = this.XamlRoot
                };
                await errDialog.ShowAsync();
            }
        }

        public Visibility HasScreenshot(string path) => string.IsNullOrEmpty(path) ? Visibility.Collapsed : Visibility.Visible;

        private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            // 1. Recupera l'allegato specifico dal mittente (sender) dell'evento
            if (sender is FrameworkElement element && element.DataContext is TicketAllegato allegato)
            {
                if (string.IsNullOrEmpty(allegato.Path)) return;

                // 2. Componi l'URL usando il path dell'allegato specifico
                string fullUrl = $"{ApiConfig.BaseUrl}/{allegato.Path.Replace("\\", "/")}";

                var dialog = new ContentDialog
                {
                    Title = "Allegato",
                    CloseButtonText = "Chiudi",
                    XamlRoot = this.XamlRoot,
                    Content = new Image
                    {
                        Source = new BitmapImage(new Uri(fullUrl)),
                        MaxHeight = 600,
                        Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
                    }
                };
                await dialog.ShowAsync();
            }
        }
    }
}