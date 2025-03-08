using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FileManager.Converters
{
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLower() switch
                {
                    "success" => new SolidColorBrush(Colors.Green),
                    "failed" => new SolidColorBrush(Colors.Red),
                    "uploading" => new SolidColorBrush(Colors.Blue),
                    "deleting" => new SolidColorBrush(Colors.Orange),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 