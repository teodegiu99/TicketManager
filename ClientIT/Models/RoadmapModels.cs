using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ClientIT.Models
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

        // Riferimento generico (può essere PhaseViewModel o altro)
        public object OriginalItem { get; set; }
    }

    public class TimelineLabel
    {
        public string Text { get; set; }
        public Thickness Margin { get; set; }
    }
}