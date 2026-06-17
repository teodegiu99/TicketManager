using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace ClientIT.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        return new BitmapImage(new Uri("ms-appx:///Assets/StoreLogo.png"));

                    // CORREZIONE: Non aggiungiamo BaseUrl. Usiamo il percorso di rete diretto.
                    // Esempio path: \\szblbfs01\zblb$\...
                    return new BitmapImage(new Uri(path));
                }
                catch (Exception)
                {
                    // Se il percorso non è valido o l'immagine non è accessibile, ritorna null
                    // (mostrerà il riquadro vuoto o un placeholder se impostato)
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}