using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace ClientIT.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        // In ClientIT/Converters/PathToImageConverter.cs
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                // Componi l'URL usando la BaseUrl definita in ApiConfig
                string fullUrl = $"{TicketManager.ApiConfig.BaseUrl}/{path.Replace("\\", "/")}";
                return new BitmapImage(new Uri(fullUrl));
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}