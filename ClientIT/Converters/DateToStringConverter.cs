using Microsoft.UI.Xaml.Data;
using System;

namespace ClientIT.Converters
{
    public class DateToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime date)
            {
                // CORREZIONE DEFINITIVA
                return DateTime.SpecifyKind(date, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}