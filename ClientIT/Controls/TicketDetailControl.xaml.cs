using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;

namespace ClientIT.Controls
{
    public sealed partial class TicketDetailControl : UserControl
    {
        public TicketDetailControl()
        {
            this.InitializeComponent();
        }

        // =========================================================
        // 1. DEPENDENCY PROPERTIES
        // =========================================================

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(TicketViewModel), typeof(TicketDetailControl), new PropertyMetadata(null, OnDataChanged));

        public TicketViewModel ViewModel
        {
            get => (TicketViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

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

        private void StatoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not ComboBox cb || cb.SelectedValue is not int val) return;
            if (val != ViewModel.StatoId)
            {
                ViewModel.StatoId = val;
                TicketStateChanged?.Invoke(this, new TicketStateChangedEventArgs(ViewModel.Nticket, val));
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

        // GESTORE PER LE NOTE (Corretto il nome per matchare lo XAML)
        private void NoteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                // Notifichiamo alla MainWindow di salvare.
                // Il testo è già nel ViewModel grazie al Binding TwoWay nel XAML.
                // Usiamo "0" come valore dummy perché la MainWindow legge tutto il ViewModel.
                TicketPropertyChanged?.Invoke(this, new TicketGenericChangedEventArgs(ViewModel.Nticket, "Note", 0));
            }
        }

        // =========================================================
        // 5. UTILS E SCREENSHOT
        // =========================================================

        public string FormatDate(DateTime date) => date.ToString("dd/MM/yyyy HH:mm");
        public Visibility HasScreenshot(string path) => string.IsNullOrEmpty(path) ? Visibility.Collapsed : Visibility.Visible;

        private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ViewModel?.ScreenshotPath)) return;

            // Controlla che la porta sia corretta
            string fullUrl = $"http://localhost:5210/{ViewModel.ScreenshotPath.Replace("\\", "/")}";

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