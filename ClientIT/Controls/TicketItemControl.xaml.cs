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

        // --- 1. PROPRIETÀ DIPENDENTI ---

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(TicketViewModel), typeof(TicketItemControl),
                new PropertyMetadata(null, OnDataChanged));

        public TicketViewModel ViewModel
        {
            get => (TicketViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public static readonly DependencyProperty StatoOptionsProperty =
            DependencyProperty.Register(nameof(StatoOptions), typeof(IList<Stato>), typeof(TicketItemControl),
                new PropertyMetadata(null, OnDataChanged));

        public IList<Stato> StatoOptions
        {
            get => (IList<Stato>)GetValue(StatoOptionsProperty);
            set => SetValue(StatoOptionsProperty, value);
        }

        public static readonly DependencyProperty AssigneeOptionsProperty =
            DependencyProperty.Register(nameof(AssigneeOptions), typeof(IList<ItUtente>), typeof(TicketItemControl),
                new PropertyMetadata(null, OnDataChanged));

        public IList<ItUtente> AssigneeOptions
        {
            get => (IList<ItUtente>)GetValue(AssigneeOptionsProperty);
            set => SetValue(AssigneeOptionsProperty, value);
        }

        // --- 2. GESTIONE AGGIORNAMENTO ---

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

            // --- GESTIONE STATO ---
            if (StatoCombo != null && StatoOptions != null)
            {
                if (StatoCombo.ItemsSource == null || StatoCombo.ItemsSource != StatoOptions)
                {
                    StatoCombo.ItemsSource = StatoOptions;
                }

                StatoCombo.SelectionChanged -= StatoComboBox_SelectionChanged;
                StatoCombo.SelectedValue = ViewModel.StatoId;
                StatoCombo.SelectionChanged += StatoComboBox_SelectionChanged;
            }

            // --- GESTIONE ASSEGNATARIO ---
            if (AssegnatoCombo != null && AssigneeOptions != null)
            {
                if (AssegnatoCombo.ItemsSource == null || AssegnatoCombo.ItemsSource != AssigneeOptions)
                {
                    AssegnatoCombo.ItemsSource = AssigneeOptions;
                }

                AssegnatoCombo.SelectionChanged -= AssegnatoaComboBox_SelectionChanged;

                var val = ViewModel.AssegnatoaId ?? 0;
                AssegnatoCombo.SelectedValue = val;

                AssegnatoCombo.SelectionChanged += AssegnatoaComboBox_SelectionChanged;
            }
        }

        // --- HELPER PER FORMATTAZIONE DATA (Fix WMC1110) ---
        public string FormatDate(DateTime date)
        {
            return date.ToString("dd/MM/yyyy HH:mm");
        }

        // --- 3. EVENTI ESTERNI ---

        public event EventHandler<TicketStateChangedEventArgs>? TicketStateChanged;
        public event EventHandler<TicketAssigneeChangedEventArgs>? TicketAssigneeChanged;

        // --- 4. HANDLERS UI ---

        private void StatoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null) return;

            if (sender is ComboBox comboBox && comboBox.SelectedValue is int nuovoId)
            {
                if (nuovoId == ViewModel.StatoId) return;

                ViewModel.StatoId = nuovoId;
                TicketStateChanged?.Invoke(this, new TicketStateChangedEventArgs(ViewModel.Nticket, nuovoId));
            }
        }

        private void AssegnatoaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null) return;

            if (sender is ComboBox comboBox && comboBox.SelectedValue is int nuovoId)
            {
                int? idConfronto = (nuovoId == 0) ? null : nuovoId;
                if (nuovoId == (ViewModel.AssegnatoaId ?? 0)) return;

                ViewModel.AssegnatoaId = idConfronto;
                TicketAssigneeChanged?.Invoke(this, new TicketAssigneeChangedEventArgs(ViewModel.Nticket, nuovoId));
            }
        }
    }

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
}