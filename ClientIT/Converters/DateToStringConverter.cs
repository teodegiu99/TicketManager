using Microsoft.UI.Xaml.Data;
using System;

namespace ClientIT.Converters
{
    public class DateToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dt)
            {
                return dt.ToLocalTime().ToString("dd/MM/yyyy");
            }
            if (value is DateTimeOffset dto)
            {
                return dto.ToLocalTime().ToString("dd/MM/yyyy");
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}