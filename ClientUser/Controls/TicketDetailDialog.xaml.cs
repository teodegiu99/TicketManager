using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TicketManager;

namespace ClientUser
{
    public sealed partial class TicketDetailDialog : UserControl
    {
        public TicketDto Ticket { get; }

        // Costruiamo l'URL completo per l'immagine
        public string ScreenshotUrl => !string.IsNullOrEmpty(Ticket.ScreenshotPath)
            ? $"{ApiConfig.BaseUrl}/{Ticket.ScreenshotPath.Replace("\\", "/")}"
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