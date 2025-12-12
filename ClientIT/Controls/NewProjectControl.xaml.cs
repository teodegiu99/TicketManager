using ClientIT.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ClientIT.Controls
{
    public sealed partial class NewProjectControl : UserControl
    {
        // Collezione osservabile per il binding e il drag & drop
        public ObservableCollection<PhaseViewModel> Phases { get; } = new();

        // Dati di riferimento passati dalla MainWindow
        private List<ItUtente> _allUsers = new();
        private List<Stato> _allStati = new();

        public NewProjectControl()
        {
            this.InitializeComponent();
            PhasesListView.ItemsSource = Phases;
        }

        // Metodo aggiornato per ricevere i dati necessari
        public void SetupData(IList<Tipologia> tipologie, IList<Urgenza> urgenze, IList<string> sedi, IList<string> adUsers)
        {
            // Nota: Queste liste servono per altre parti o potrebbero servire in futuro.
            // Qui abbiamo bisogno specificamente di Utenti IT e Stati per le fasi.
            // Possiamo recuperarli dalle proprietà statiche o passarli esplicitamente se modifichiamo la firma.
            // Per ora assumiamo che MainWindow passi le liste corrette.
        }

        // OVERLOAD: Usa questo per passare i dati specifici per le fasi
        public void SetupReferenceData(List<ItUtente> users, List<Stato> stati)
        {
            _allUsers = users;
            _allStati = stati;
        }

        private async void BtnAddPhase_Click(object sender, RoutedEventArgs e)
        {
            var dialogContent = new PhaseDetailDialog();
            dialogContent.Setup(_allUsers, _allStati);

            var dialog = new ContentDialog
            {
                Title = "Nuova Fase",
                Content = dialogContent,
                PrimaryButtonText = "Aggiungi",
                CloseButtonText = "Annulla",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (dialogContent.Validate())
                {
                    var newPhase = dialogContent.GetPhase();
                    Phases.Add(newPhase);
                }
            }
        }

        private async void PhasesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PhaseViewModel phase)
            {
                var dialogContent = new PhaseDetailDialog();
                dialogContent.Setup(_allUsers, _allStati, phase);

                var dialog = new ContentDialog
                {
                    Title = "Modifica Fase",
                    Content = dialogContent,
                    PrimaryButtonText = "Salva",
                    CloseButtonText = "Annulla",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    if (dialogContent.Validate())
                    {
                        var updatedPhase = dialogContent.GetPhase();
                        // Aggiorna l'oggetto esistente
                        phase.Titolo = updatedPhase.Titolo;
                        phase.Descrizione = updatedPhase.Descrizione;
                        phase.DataInizio = updatedPhase.DataInizio;
                        phase.DataPrevFine = updatedPhase.DataPrevFine;
                        phase.AssegnatoA = updatedPhase.AssegnatoA;
                        phase.Stato = updatedPhase.Stato;
                    }
                }
            }
        }

        private void BtnRemovePhase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PhaseViewModel phase)
            {
                Phases.Remove(phase);
            }
        }

        private void BtnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            // Logica di salvataggio (da implementare con API)
            // Per salvare l'ordine corretto, iteriamo sulla lista Phases che riflette l'ordine visivo
            int order = 0;
            foreach (var phase in Phases)
            {
                phase.Ordine = order++;
            }

            // ... chiama API POST ...
        }
    }
}