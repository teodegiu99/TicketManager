using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TicketManager;
using System.Linq;

namespace ClientUser
{
    public sealed partial class TicketDetailDialog : UserControl
    {
        public TicketDto Ticket { get; }

        // Sostituiamo la vecchia proprietà singola con il controllo sulla lista
        public Visibility HasScreenshots => (Ticket.ScreenshotPaths != null && Ticket.ScreenshotPaths.Any()) 
            ? Visibility.Visible 
            : Visibility.Collapsed;

        public Visibility HasNotes => !string.IsNullOrEmpty(Ticket.Note) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasPerContoDi => !string.IsNullOrEmpty(Ticket.PerContoDi) ? Visibility.Visible : Visibility.Collapsed;

        public TicketDetailDialog(TicketDto ticket)
        {
            this.Ticket = ticket;
            this.InitializeComponent();
        }
    }
}