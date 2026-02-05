using ClientIT.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace ClientIT.Controls
{
    public sealed partial class AddDocDialog : ContentDialog
    {
        // Collezione per i badge
        public ObservableCollection<string> ViewModelKeywords { get; } = new();

        public AddDocDialog(List<Tipologia> tipologie, string soluzionePrecompilata, string titoloDefault)
        {
            this.InitializeComponent();

            // Popola Combobox
            CmbCategoria.ItemsSource = tipologie;
            if (tipologie.Any()) CmbCategoria.SelectedIndex = 0;

            // Precompila Titolo
            TxtTitolo.Text = titoloDefault;

            // Precompila Soluzione (RichEditBox usa Document.SetText)
            if (!string.IsNullOrEmpty(soluzionePrecompilata))
            {
                RtbSoluzione.Document.SetText(TextSetOptions.None, soluzionePrecompilata);
            }
        }

        // Evento tasto "Invio" su textbox keyword
        private void TxtNewKeyword_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AddKeyword();
                e.Handled = true;
            }
        }

        private void BtnAddKeyword_Click(object sender, RoutedEventArgs e)
        {
            AddKeyword();
        }

        private void AddKeyword()
        {
            string text = TxtNewKeyword.Text.Trim();
            if (!string.IsNullOrEmpty(text) && !ViewModelKeywords.Contains(text))
            {
                ViewModelKeywords.Add(text);
            }
            TxtNewKeyword.Text = ""; // Pulisci input
            TxtNewKeyword.Focus(FocusState.Programmatic);
        }

        // Rimuovi keyword (click sulla X)
        private void BtnRemoveKeyword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string keyToRemove)
            {
                ViewModelKeywords.Remove(keyToRemove);
            }
        }

        // Metodo pubblico per recuperare i dati inseriti
        public (string Titolo, int CategoriaId, string Soluzione, string Query, List<string> Keywords) GetResult()
        {
            // Recupera testo da RichEditBox
            RtbSoluzione.Document.GetText(TextGetOptions.None, out string soluzione);

            return (
                TxtTitolo.Text,
                (int)(CmbCategoria.SelectedValue ?? 0),
                soluzione,
                TxtQuery.Text,
                ViewModelKeywords.ToList()
            );
        }
    }
}