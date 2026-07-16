using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DisplayLight.App.Presentation.Controls;

public sealed class DiscretePresetListBox : ListBox
{
    public static readonly DependencyProperty CurrentIndexProperty = DependencyProperty.Register(
        nameof(CurrentIndex),
        typeof(int?),
        typeof(DiscretePresetListBox),
        new PropertyMetadata(null));

    public static readonly DependencyProperty HasPendingChangeProperty = DependencyProperty.Register(
        nameof(HasPendingChange),
        typeof(bool),
        typeof(DiscretePresetListBox),
        new PropertyMetadata(false));

    public int? CurrentIndex
    {
        get => (int?)GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    public bool HasPendingChange
    {
        get => (bool)GetValue(HasPendingChangeProperty);
        set => SetValue(HasPendingChangeProperty, value);
    }
}

public sealed class CurrentPresetMarkerVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 || values[0] is not int itemIndex || values[2] is not true)
        {
            return Visibility.Collapsed;
        }

        int? currentIndex = values[1] is int value ? value : null;

        return currentIndex == itemIndex ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
