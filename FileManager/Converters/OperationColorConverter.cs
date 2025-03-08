using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FileManager.Models;

namespace FileManager.Converters
{
    public class OperationColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OperationType operation)
            {
                return operation switch
                {
                    OperationType.Upload => new SolidColorBrush(Colors.Green),
                    OperationType.Delete => new SolidColorBrush(Colors.Red),
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