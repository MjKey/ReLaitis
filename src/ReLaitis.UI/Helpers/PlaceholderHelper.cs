using System.Windows;

namespace ReLaitis.UI.Helpers;

/// <summary>
/// Attached Property для отображения плейсхолдера (текста-подсказки) в TextBox.
/// </summary>
public static class PlaceholderHelper
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(PlaceholderHelper),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static string GetPlaceholder(DependencyObject obj) => (string)obj.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject obj, string value) => obj.SetValue(PlaceholderProperty, value);
}
