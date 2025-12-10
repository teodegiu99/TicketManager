using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace ClientUser
{
    public sealed partial class TicketDetailDialog : UserControl
    {
        public TicketDto Ticket { get; }

        // Costruiamo l'URL completo per l'immagine
        public string ScreenshotUrl => !string.IsNullOrEmpty(Ticket.ScreenshotPath)
            ? $"http://localhost:5210/{Ticket.ScreenshotPath.Replace("\\", "/")}"
            : string.Empty;

        // Proprietà per la visibilità condizionale
        public Visibility HasScreenshot => !string.IsNullOrEmpty(Ticket.ScreenshotPath) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasNotes => !string.IsNullOrEmpty(Ticket.Note) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasPerContoDi => !string.IsNullOrEmpty(Ticket.PerContoDi) ? Visibility.Visible : Visibility.Collapsed;

        public TicketDetailDialog(TicketDto ticket)
        {
            this.Ticket = ticket;
            this.InitializeComponent();
        }
    }
}