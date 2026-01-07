using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using ClientIT.Models; // Assicurati che PhaseViewModel sia visibile qui

namespace ClientIT.Controls // O ClientIT.Models, ma se usi Controls non devi cambiare gli using
{
    public class RoadmapItem
    {
        public string Titolo { get; set; } = string.Empty;
        public Thickness Margin { get; set; }
        public Thickness TextMargin { get; set; }
        public double Width { get; set; }
        public SolidColorBrush Color { get; set; }
        public string TooltipText { get; set; } = string.Empty;
        public string DateText { get; set; } = string.Empty;
        public PhaseViewModel OriginalPhase { get; set; }
    }

    public class TimelineLabel
    {
        public string Text { get; set; }
        public Thickness Margin { get; set; }
    }
}