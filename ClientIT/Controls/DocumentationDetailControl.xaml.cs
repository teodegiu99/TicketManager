using ClientIT.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input; // Necessario per KeyRoutedEventArgs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using TicketManager;
using Windows.Storage.Pickers;

namespace ClientIT.Controls
{
    public sealed partial class DocumentationDetailControl : UserControl
    {
        public event EventHandler BackRequested;
        public event EventHandler DataSaved;

        private DocumentazioneDto _currentDoc;
        private HttpClient _client;

        // Collezione per gestire le keyword nell'interfaccia
        private ObservableCollection<string> _currentKeywords = new();

        public DocumentationDetailControl()
        {
            this.InitializeComponent();
            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true };
            _client = new HttpClient(handler);
        }

        public void Load(DocumentazioneDto doc, List<Tipologia> categorie)
        {
            _currentDoc = doc;

            // 1. Caricamento Campi Testo
            TxtTitolo.Text = doc.Titolo ?? "";
            TxtQuery.Text = doc.Query ?? "";

            // 2. Caricamento Keywords
            _currentKeywords.Clear();
            if (doc.KeywordNomi != null)
            {
                foreach (var k in doc.KeywordNomi) _currentKeywords.Add(k);
            }
            IcKeywords.ItemsSource = _currentKeywords;

            // 3. Caricamento ComboBox (FIX: Reset ItemsSource per refresh forzato)
            if (categorie != null)
            {
                // Rimuovi binding precedente se esiste
                CmbCategoria.ItemsSource = null;
                CmbCategoria.ItemsSource = categorie;

                // Imposta l'item selezionato
                if (doc.CategoriaId > 0)
                {
                    CmbCategoria.SelectedValue = doc.CategoriaId;
                }
                else
                {
                    CmbCategoria.SelectedIndex = -1;
                }
            }

            // 4. Caricamento Editor (RTF)
            if (!string.IsNullOrEmpty(doc.Soluzione))
            {
                try
                {
                    RtbSoluzione.Document.SetText(TextSetOptions.FormatRtf, doc.Soluzione);
                }
                catch
                {
                    RtbSoluzione.Document.SetText(TextSetOptions.None, doc.Soluzione);
                }
            }
            else
            {
                RtbSoluzione.Document.SetText(TextSetOptions.None, "");
            }
        }



        // --- Gestione Keywords ---

        private void BtnAddKeyword_Click(object sender, RoutedEventArgs e) => AddKeyword();

        private void TxtNewKeyword_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AddKeyword();
                e.Handled = true;
            }
        }

        private void AddKeyword()
        {
            var txt = TxtNewKeyword.Text.Trim();
            if (!string.IsNullOrEmpty(txt) && !_currentKeywords.Contains(txt))
            {
                _currentKeywords.Add(txt);
                TxtNewKeyword.Text = "";
            }
        }

        private void BtnRemoveKeyword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string k)
            {
                _currentKeywords.Remove(k);
            }
        }

        // --- Editor Toolbar ---
        private void Editor_Bold_Click(object sender, RoutedEventArgs e)
        {
            var selectedText = RtbSoluzione.Document.Selection;
            if (selectedText != null)
            {
                var charFormatting = selectedText.CharacterFormat;
                charFormatting.Bold = FormatEffect.Toggle;
                selectedText.CharacterFormat = charFormatting;
            }
        }

        private void Editor_Italic_Click(object sender, RoutedEventArgs e)
        {
            var selectedText = RtbSoluzione.Document.Selection;
            if (selectedText != null)
            {
                var charFormatting = selectedText.CharacterFormat;
                charFormatting.Italic = FormatEffect.Toggle;
                selectedText.CharacterFormat = charFormatting;
            }
        }

        private async void Editor_AddImage_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            // Recupera la finestra pubblica definita in App.xaml.cs
            var window = (Application.Current as App)?.m_window;
            if (window != null)
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            }

            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    RtbSoluzione.Document.Selection.InsertImage(600, 400, 0, VerticalCharacterAlignment.Baseline, "Image", stream);
                }
            }
        }


        private async void Editor_AddPdf_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new Windows.Storage.Pickers.FileOpenPicker();
            var window = (Application.Current as App)?.m_window;
            if (window != null)
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            }

            openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".pdf");

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    string targetFolder = @"\\szblbfs01\zblb$\group_utenti\Inter_Uffici\Ticketmanager";
                    string fileName = $"DocPdf_{System.Guid.NewGuid().ToString().Substring(0,8)}.pdf";
                    string destPath = System.IO.Path.Combine(targetFolder, fileName);
                    
                    System.IO.File.Copy(file.Path, destPath, true);

                    RtbSoluzione.Document.Selection.Text = $"[Apri PDF: {file.Name}]";
                    RtbSoluzione.Document.Selection.Link = $"\"{destPath}\"";
                    RtbSoluzione.Document.Selection.StartPosition = RtbSoluzione.Document.Selection.EndPosition;
                    RtbSoluzione.Document.Selection.Text = "\n";
                }
                catch(System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore caricamento PDF: {ex.Message}");
                }
            }
        }

        // --- Salvataggio ---

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            RtbSoluzione.Document.GetText(TextGetOptions.FormatRtf, out string rtfContent);
            if (rtfContent != null) rtfContent = rtfContent.Replace("\0", "").Trim();

            int selectedCatId = 0;
            if (CmbCategoria.SelectedValue is int id) selectedCatId = id;

            // Crea il DTO
            var docDto = new DocumentazioneDto
            {
                Id = _currentDoc.Id, // Se è 0, il backend creerà un nuovo ID
                Titolo = TxtTitolo.Text,
                CategoriaId = selectedCatId,
                Soluzione = rtfContent,
                Query = TxtQuery.Text,
                KeywordNomi = _currentKeywords.ToList(),
                Nticket = _currentDoc.Nticket, // Mantiene 0 se nuovo, o il vecchio se esiste

                CategoriaNome = null,
                CategoriaColore = null,
                KeywordIds = null
            };

            try
            {
                HttpResponseMessage response;

                // SE L'ID È 0 => CREAZIONE (POST)
                if (_currentDoc.Id == 0)
                {
                    var createReq = new ClientIT.Models.CreateDocRequest { Nticket = _currentDoc.Nticket, Titolo = TxtTitolo.Text, Soluzione = rtfContent, Query = TxtQuery.Text, CategoriaId = selectedCatId, Keywords = _currentKeywords.ToList() }; response = await _client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/api/documentazione", createReq);
                }
                // ALTRIMENTI => AGGIORNAMENTO (PUT)
                else
                {
                    response = await _client.PutAsJsonAsync($"{ApiConfig.BaseUrl}/api/documentazione/{_currentDoc.Id}", docDto);
                }

                if (response.IsSuccessStatusCode)
                {
                    DataSaved?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Errore salvataggio: {error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Eccezione salvataggio: {ex.Message}");
            }
        }
        private void BtnCopyQuery_Click(object sender, RoutedEventArgs e)
        {
            var queryText = TxtQuery.Text;
            if (!string.IsNullOrWhiteSpace(queryText))
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(queryText);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);

                // Opzionale: Cambia temporaneamente l'icona o mostra un feedback visivo
                ShowCopyFeedback();
            }
        }

        private async void ShowCopyFeedback()
        {
            // Cambia l'icona in "Completato" (Checkmark) per un secondo
            if (BtnCopyQuery.Content is FontIcon icon)
            {
                string oldGlyph = icon.Glyph;
                icon.Glyph = "\uE73E"; // Icona Checkmark
                await System.Threading.Tasks.Task.Delay(1500);
                icon.Glyph = oldGlyph;
            }
        }
        private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
    }
}

