using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ReLaitis.Core.Enums;
using MediaColor = System.Windows.Media.Color;

namespace ReLaitis.UI.Helpers;

public static class ActionVisuals
{
    public static (string Label, MediaColor Bg, MediaColor Fg, MediaColor Border) GetVisuals(ActionType type)
    {
        return type switch
        {
            ActionType.IfProcessSelected or
            ActionType.IfProcessExists or
            ActionType.IfVariableValue or
            ActionType.IfWebsiteSelected or
            ActionType.IfWebsiteNavValue =>
                ("ЕСЛИ", MediaColor.FromArgb(45, 245, 158, 11), MediaColor.FromRgb(251, 191, 36), MediaColor.FromRgb(245, 158, 11)),

            ActionType.Else =>
                ("ИНАЧЕ", MediaColor.FromArgb(45, 217, 119, 6), MediaColor.FromRgb(253, 230, 138), MediaColor.FromRgb(217, 119, 6)),

            ActionType.Loop =>
                ("ЦИКЛ", MediaColor.FromArgb(45, 99, 102, 241), MediaColor.FromRgb(165, 180, 252), MediaColor.FromRgb(99, 102, 241)),

            ActionType.Break =>
                ("BREAK", MediaColor.FromArgb(45, 244, 63, 94), MediaColor.FromRgb(253, 164, 175), MediaColor.FromRgb(244, 63, 94)),

            ActionType.EndBlock =>
                ("КОНЕЦ БЛОКА", MediaColor.FromArgb(30, 71, 85, 105), MediaColor.FromRgb(148, 163, 184), MediaColor.FromRgb(71, 85, 105)),

            ActionType.RandomActionBlock =>
                ("СЛУЧАЙНО", MediaColor.FromArgb(45, 168, 85, 247), MediaColor.FromRgb(192, 132, 252), MediaColor.FromRgb(168, 85, 247)),

            ActionType.SetVariableValue =>
                ("ПЕРЕМЕННАЯ", MediaColor.FromArgb(45, 14, 165, 233), MediaColor.FromRgb(56, 189, 248), MediaColor.FromRgb(14, 165, 233)),

            ActionType.OpenURL or
            ActionType.WebPageClick or
            ActionType.WebPageFocus or
            ActionType.WebPageGetText or
            ActionType.WebPageNavigate or
            ActionType.WebPageNavClick or
            ActionType.WebPagePopupOpen or
            ActionType.WebPageScript or
            ActionType.HttpWebRequest or
            ActionType.GetUrlSelectorText =>
                ("ВЕБ", MediaColor.FromArgb(40, 59, 130, 246), MediaColor.FromRgb(96, 165, 250), MediaColor.FromRgb(59, 130, 246)),

            ActionType.MouseMoveOn =>
                ("ЭЛЕМЕНТ", MediaColor.FromArgb(45, 20, 184, 166), MediaColor.FromRgb(45, 212, 191), MediaColor.FromRgb(20, 184, 166)),

            ActionType.MouseButton or
            ActionType.MouseMove or
            ActionType.MouseScroll =>
                ("МЫШЬ", MediaColor.FromArgb(40, 168, 85, 247), MediaColor.FromRgb(192, 132, 252), MediaColor.FromRgb(168, 85, 247)),

            ActionType.Hotkeys or
            ActionType.TypeText =>
                ("ВВОД", MediaColor.FromArgb(40, 139, 92, 246), MediaColor.FromRgb(196, 181, 253), MediaColor.FromRgb(139, 92, 246)),

            ActionType.OpenFile or
            ActionType.CloseApp or
            ActionType.ShowWindow or
            ActionType.BatchScript =>
                ("СИСТЕМА", MediaColor.FromArgb(40, 16, 185, 129), MediaColor.FromRgb(52, 211, 153), MediaColor.FromRgb(16, 185, 129)),

            ActionType.CSharpScript =>
                ("C# КОД", MediaColor.FromArgb(45, 147, 51, 234), MediaColor.FromRgb(216, 180, 254), MediaColor.FromRgb(147, 51, 234)),

            ActionType.Say or
            ActionType.PlayAudio or
            ActionType.Notify or
            ActionType.VoiceCommand or
            ActionType.WaitNextCommand =>
                ("АУДИО", MediaColor.FromArgb(40, 245, 158, 11), MediaColor.FromRgb(251, 191, 36), MediaColor.FromRgb(245, 158, 11)),

            ActionType.Comment =>
                ("КОММЕНТАРИЙ", MediaColor.FromArgb(30, 100, 116, 139), MediaColor.FromRgb(148, 163, 184), MediaColor.FromRgb(71, 85, 105)),

            ActionType.Pause =>
                ("ПАУЗА", MediaColor.FromArgb(40, 100, 116, 139), MediaColor.FromRgb(203, 213, 225), MediaColor.FromRgb(100, 116, 139)),

            ActionType.JetAim =>
                ("JETAIM", MediaColor.FromArgb(40, 244, 63, 94), MediaColor.FromRgb(251, 113, 133), MediaColor.FromRgb(244, 63, 94)),

            _ =>
                ("ДЕЙСТВИЕ", MediaColor.FromArgb(40, 100, 116, 139), MediaColor.FromRgb(148, 163, 184), MediaColor.FromRgb(51, 65, 85))
        };
    }
}

public class ActionTypeToBadgeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ActionType type)
            return ActionVisuals.GetVisuals(type).Label;
        return "ДЕЙСТВИЕ";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ActionTypeToBadgeBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ActionType type)
            return new SolidColorBrush(ActionVisuals.GetVisuals(type).Bg);
        return new SolidColorBrush(MediaColor.FromArgb(40, 100, 116, 139));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ActionTypeToBadgeFgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ActionType type)
            return new SolidColorBrush(ActionVisuals.GetVisuals(type).Fg);
        return new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
