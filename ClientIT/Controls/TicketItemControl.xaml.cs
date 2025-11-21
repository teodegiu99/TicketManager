using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace ClientIT.Controls
{
    public sealed partial class TicketItemControl : UserControl
    {
        public TicketItemControl()
        {
            this.InitializeComponent();
        }

        // --- 1. PROPRIETÀ DIPENDENTI (ViewModel & Liste) ---

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(TicketViewModel), typeof(TicketItemControl),
                new PropertyMetadata(null, OnDataChanged));

        public TicketViewModel ViewModel
        {
            get => (TicketViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        // Lista Stati
        public static readonly DependencyProperty StatoOptionsProperty =
            DependencyProperty.Register(nameof(StatoOptions), typeof(IList<Stato>), typeof(TicketItemControl),
                new PropertyMetadata(null, OnDataChanged));
        public IList<Stato> StatoOptions
        {
            get => (IList<Stato>)GetValue(StatoOptionsProperty);
            set => SetValue(StatoOptionsProperty, value);
        }

        // Lista Utenti IT
        public static readonly DependencyProperty AssigneeOptionsProperty =
            DependencyProperty.Register(nameof(AssigneeOptions), typeof(IList<ItUtente>), typeof(TicketItemControl),
                new PropertyMetadata(null, OnDataChanged));
        public IList<ItUtente> AssigneeOptions
        {
            get => (IList<ItUtente>)GetValue(AssigneeOptionsProperty);
            set => SetValue(AssigneeOptionsProperty, value);
        }

        // CORREZIONE: Deve essere IList<Tipologia>, NON IList<object>
        // Controlla attentamente il "typeof(IList<Tipologia>)" nel Register qui sotto
        public static readonly DependencyProperty TipologiaOptionsProperty =
            DependencyProperty.Register(nameof(TipologiaOptions), typeof(IList<Tipologia>), typeof(TicketItemControl),
                new PropertyMetadata(null, OnDataChanged));
        public IList<Tipologia> TipologiaOptions
        {
            get => (IList<Tipologia>)GetValue(TipologiaOptionsProperty);
            set => SetValue(TipologiaOptionsProperty, value);
        }

        // CORREZIONE: Deve essere IList<Urgenza>, NON IList<object>
        public static readonly DependencyProperty UrgenzaOptionsProperty =
            DependencyProperty.Register(nameof(UrgenzaOptions), typeof(IList<Urgenza>), typeof(TicketItemControl),
                new PropertyMetadata(null, OnDataChanged));
        public IList<Urgenza> UrgenzaOptions
        {
            get => (IList<Urgenza>)GetValue(UrgenzaOptionsProperty);
            set => SetValue(UrgenzaOptionsProperty, value);
        }

        // --- 2. GESTIONE AGGIORNAMENTO UI ---

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TicketItemControl control)
            {
                control.UpdateSelections();
            }
        }

        private void UpdateSelections()
        {
            if (ViewModel == null) return;

            // Helper: usa IEnumerable per accettare le liste generiche senza problemi di cast
            void UpdateCombo(ComboBox combo, System.Collections.IEnumerable items, object? currentValue, SelectionChangedEventHandler handler)
            {
                if (combo == null) return;

                if (items != null && (combo.ItemsSource == null || combo.ItemsSource != items))
                {
                    combo.ItemsSource = items;
                }

                combo.SelectionChanged -= handler;
                combo.SelectedValue = currentValue;
                combo.SelectionChanged += handler;
            }

            // 1. Tipologia
            UpdateCombo(TipologiaCombo, TipologiaOptions, ViewModel.TipologiaId, TipologiaComboBox_SelectionChanged);

            // 2. Urgenza
            UpdateCombo(UrgenzaCombo, UrgenzaOptions, ViewModel.UrgenzaId, UrgenzaComboBox_SelectionChanged);

            // 3. Stato
            UpdateCombo(StatoCombo, StatoOptions, ViewModel.StatoId, StatoComboBox_SelectionChanged);

            // 4. Assegnatario
            UpdateCombo(AssegnatoCombo, AssigneeOptions, ViewModel.AssegnatoaId ?? 0, AssegnatoaComboBox_SelectionChanged);
        }

        public string FormatDate(DateTime date) => date.ToString("dd/MM/yyyy HH:mm");

        // --- 3. EVENTI ---

        public event EventHandler<TicketStateChangedEventArgs>? TicketStateChanged;
        public event EventHandler<TicketAssigneeChangedEventArgs>? TicketAssigneeChanged;
        public event EventHandler<TicketGenericChangedEventArgs>? TicketPropertyChanged;

        // --- 4. HANDLERS UI ---

        private void TipologiaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not ComboBox cb || cb.SelectedValue is not int newVal) return;

            if (newVal != ViewModel.TipologiaId)
            {
                ViewModel.TipologiaId = newVal;
                TicketPropertyChanged?.Invoke(this, new TicketGenericChangedEventArgs(ViewModel.Nticket, "TipologiaId", newVal));
            }
        }

        private void UrgenzaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not ComboBox cb || cb.SelectedValue is not int newVal) return;

            if (newVal != ViewModel.UrgenzaId)
            {
                ViewModel.UrgenzaId = newVal;
                TicketPropertyChanged?.Invoke(this, new TicketGenericChangedEventArgs(ViewModel.Nticket, "UrgenzaId", newVal));
            }
        }

        private void StatoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not ComboBox cb || cb.SelectedValue is not int newVal) return;

            if (newVal != ViewModel.StatoId)
            {
                ViewModel.StatoId = newVal;
                TicketStateChanged?.Invoke(this, new TicketStateChangedEventArgs(ViewModel.Nticket, newVal));
            }
        }

        private void AssegnatoaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not ComboBox cb || cb.SelectedValue is not int newVal) return;

            int? idLogico = newVal == 0 ? null : newVal;

            if (newVal != (ViewModel.AssegnatoaId ?? 0))
            {
                ViewModel.AssegnatoaId = idLogico;
                TicketAssigneeChanged?.Invoke(this, new TicketAssigneeChangedEventArgs(ViewModel.Nticket, newVal));
            }
        }
    }

    // Classi eventi
    public class TicketStateChangedEventArgs : EventArgs
    {
        public int Nticket { get; }
        public int StatoId { get; }
        public TicketStateChangedEventArgs(int n, int s) { Nticket = n; StatoId = s; }
    }

    public class TicketAssigneeChangedEventArgs : EventArgs
    {
        public int Nticket { get; }
        public int AssegnatoaId { get; }
        public TicketAssigneeChangedEventArgs(int n, int a) { Nticket = n; AssegnatoaId = a; }
    }

    public class TicketGenericChangedEventArgs : EventArgs
    {
        public int Nticket { get; }
        public string PropertyName { get; }
        public int NewValue { get; }
        public TicketGenericChangedEventArgs(int n, string prop, int val)
        {
            Nticket = n;
            PropertyName = prop;
            NewValue = val;
        }
    }
}