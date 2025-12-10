using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace ClientUser.Converters // Assicurati che il namespace sia corretto
{
    public class StringToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var path = value as string;

            if (string.IsNullOrEmpty(path))
            {
                // Opzionale: Ritorna un'immagine di placeholder se il path è null
                // return new BitmapImage(new Uri("ms-appx:///Assets/placeholder.png"));
                return null;
            }

            try
            {
                // Se è un URL web
                if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    return new BitmapImage(new Uri(path));
                }

                // Se è un percorso locale assoluto, WinUI potrebbe avere problemi di permessi 
                // se l'app è sandboxed, ma per il debug prova questo:
                return new BitmapImage(new Uri(path));
            }
            catch
            {
                // Se il percorso non è valido, ritorna null o placeholder
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}