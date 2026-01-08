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
                    // Verifica veloce se il file esiste (opzionale, ma evita errori di caricamento)
                    if (System.IO.File.Exists(path))
                    {
                        // Crea una BitmapImage dal percorso file
                        return new BitmapImage(new Uri(path));
                    }
                }
                catch
                {
                    // Se fallisce (es. disco S: non connesso), non ritorna nulla
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