using System.Globalization;
using System.Windows.Data;

namespace FileTidy.App.Converters;

/// <summary>布尔取反转换器（供「Busy 时禁用按钮」绑定 IsEnabled 取反）</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : throw new ArgumentOutOfRangeException(nameof(value), "值必须为 bool");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
