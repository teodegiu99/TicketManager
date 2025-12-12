using ClientIT.Models;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace ClientIT.Controls
{
    public sealed partial class PhaseDetailDialog : UserControl
    {
        // Definiamo l'oggetto speciale per l'utente esterno
        private readonly ItUtente _utenteEsterno = new ItUtente { Id = -999, Nome = "Utente Esterno", UsernameAd = "EXTERNAL" };

        public PhaseDetailDialog()
        {
            this.InitializeComponent();
        }

        public void Setup(List<ItUtente> users, List<Stato> stati, PhaseViewModel? existingPhase = null)
        {
            // 1. Prepara la lista combinata per AssegnatoA
            var comboUsers = new List<ItUtente>();

            // Aggiungi opzione "Non assegnato" (Id 0 o null) se non c'è già
            var nonAssegnato = users.FirstOrDefault(u => u.Id == 0);
            if (nonAssegnato != null) comboUsers.Add(nonAssegnato);

            // Aggiungi l'opzione speciale "Utente Esterno"
            comboUsers.Add(_utenteEsterno);

            // Aggiungi gli altri utenti IT (escluso "Non assegnato" se già aggiunto)
            comboUsers.AddRange(users.Where(u => u.Id != 0));

            CmbAssegnato.ItemsSource = comboUsers;
            CmbStato.ItemsSource = stati;

            // Default selections
            if (CmbStato.Items.Count > 0) CmbStato.SelectedIndex = 0;
            if (CmbAssegnato.Items.Count > 0) CmbAssegnato.SelectedIndex = 0;

            // 2. Carica dati esistenti (se in modifica)
            if (existingPhase != null)
            {
                TxtTitolo.Text = existingPhase.Titolo;
                TxtDescrizione.Text = existingPhase.Descrizione;
                if (existingPhase.DataInizio.HasValue)
                    DateInizio.Date = existingPhase.DataInizio.Value;

                if (existingPhase.DataPrevFine.HasValue)
                    DateFine.Date = existingPhase.DataPrevFine.Value;

                // Selezione Stato
                if (existingPhase.Stato != null)
                    CmbStato.SelectedItem = stati.FirstOrDefault(s => s.Id == existingPhase.Stato.Id);

                // Selezione Assegnato
                if (existingPhase.AssegnatoA != null)
                {
                    // Se è l'utente esterno
                    if (existingPhase.AssegnatoA.Id == -999 || existingPhase.AssegnatoA.Nome == "Utente Esterno")
                    {
                        CmbAssegnato.SelectedItem = _utenteEsterno;
                    }
                    else
                    {
                        // Cerca per ID
                        var match = comboUsers.FirstOrDefault(u => u.Id == existingPhase.AssegnatoA.Id);
                        CmbAssegnato.SelectedItem = match ?? nonAssegnato;
                    }
                }
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