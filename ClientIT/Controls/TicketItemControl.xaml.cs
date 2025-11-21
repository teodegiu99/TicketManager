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

        // --- 1. PROPRIETÀ DIPENDENTI (Props) ---
        // Usiamo il callback "OnChanged" per aggiornare la grafica quando i dati arrivano

        // Il Ticket (ViewModel)
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

        // --- 2. GESTIONE AGGIORNAMENTO GRAFICO ---

        // Questo metodo scatta ogni volta che cambia il Ticket o arrivano le Liste
        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TicketItemControl control)
            {
                control.UpdateSelections();
            }
        }

        // Forza i ComboBox a mostrare il valore corretto
        private void UpdateSelections()
        {
            if (ViewModel == null) return;

            // Aggiorna ComboBox Stato
            if (StatoCombo != null && StatoOptions != null)
            {
                // Disattiviamo l'evento per evitare che scatti l'auto-salvataggio mentre carichiamo
                StatoCombo.SelectionChanged -= StatoComboBox_SelectionChanged;
                StatoCombo.SelectedValue = ViewModel.StatoId;
                StatoCombo.SelectionChanged += StatoComboBox_SelectionChanged;
            }

            // Aggiorna ComboBox Assegnatario
            if (AssegnatoCombo != null && AssigneeOptions != null)
            {
                AssegnatoCombo.SelectionChanged -= AssegnatoaComboBox_SelectionChanged;
                // Se è null, mettiamo 0 ("Non assegnato")
                AssegnatoCombo.SelectedValue = ViewModel.AssegnatoaId ?? 0;
                AssegnatoCombo.SelectionChanged += AssegnatoaComboBox_SelectionChanged;
            }
        }

        // --- 3. EVENTI ESTERNI (Auto-Salvataggio) ---

        public event EventHandler<TicketStateChangedEventArgs>? TicketStateChanged;
        public event EventHandler<TicketAssigneeChangedEventArgs>? TicketAssigneeChanged;

        // --- 4. GESTORI EVENTI UI ---

        private void StatoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Controllo di sicurezza per evitare crash o loop
            if (ViewModel == null) return;

            if (sender is ComboBox comboBox && comboBox.SelectedValue is int nuovoId)
            {
                // Se il valore è identico, non fare nulla
                if (nuovoId == ViewModel.StatoId) return;

                // Aggiorna MANUALMENTE il ViewModel (perché nel XAML è OneWay)
                ViewModel.StatoId = nuovoId;

                // Avvisa la MainWindow di salvare
                TicketStateChanged?.Invoke(this, new TicketStateChangedEventArgs(ViewModel.Nticket, nuovoId));
            }
        }

        private void AssegnatoaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null) return;

            if (sender is ComboBox comboBox && comboBox.SelectedValue is int nuovoId)
            {
                int? idConfronto = (nuovoId == 0) ? null : nuovoId;

                if (idConfronto == ViewModel.AssegnatoaId) return;

                // Aggiorna MANUALMENTE il ViewModel
                ViewModel.AssegnatoaId = idConfronto;

                // Avvisa la MainWindow di salvare
                TicketAssigneeChanged?.Invoke(this, new TicketAssigneeChangedEventArgs(ViewModel.Nticket, nuovoId));
            }
        }
    }

    // Classi per passare i dati degli eventi
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