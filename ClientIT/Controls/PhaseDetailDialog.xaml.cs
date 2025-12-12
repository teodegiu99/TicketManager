using ClientIT.Models;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace ClientIT.Controls
{
    public sealed partial class PhaseDetailDialog : UserControl
    {
        public PhaseDetailDialog()
        {
            this.InitializeComponent();
        }

        public void Setup(List<ItUtente> users, List<Stato> stati, PhaseViewModel? existingPhase = null)
        {
            CmbAssegnato.ItemsSource = users;
            CmbStato.ItemsSource = stati;

            // Seleziona default se nuovi
            if (CmbStato.Items.Count > 0) CmbStato.SelectedIndex = 0;
            if (CmbAssegnato.Items.Count > 0) CmbAssegnato.SelectedIndex = 0; // "Non assegnato" solitamente è il primo

            if (existingPhase != null)
            {
                TxtTitolo.Text = existingPhase.Titolo;
                TxtDescrizione.Text = existingPhase.Descrizione;
                DateInizio.Date = existingPhase.DataInizio;
                DateFine.Date = existingPhase.DataPrevFine;

                if (existingPhase.Stato != null)
                    CmbStato.SelectedItem = stati.FirstOrDefault(s => s.Id == existingPhase.Stato.Id);

                if (existingPhase.AssegnatoA != null)
                    CmbAssegnato.SelectedItem = users.FirstOrDefault(u => u.Id == existingPhase.AssegnatoA.Id);
            }
        }

        public PhaseViewModel GetPhase()
        {
            return new PhaseViewModel
            {
                Titolo = TxtTitolo.Text,
                Descrizione = TxtDescrizione.Text,
                DataInizio = DateInizio.Date,
                DataPrevFine = DateFine.Date,
                Stato = CmbStato.SelectedItem as Stato,
                AssegnatoA = CmbAssegnato.SelectedItem as ItUtente
            };
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(TxtTitolo.Text))
            {
                ErrorBar.Message = "Il titolo è obbligatorio.";
                ErrorBar.IsOpen = true;
                return false;
            }
            return true;
        }
    }
}