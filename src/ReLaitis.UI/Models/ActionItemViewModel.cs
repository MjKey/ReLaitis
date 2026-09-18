using System;
using System.Windows;
using System.Windows.Media;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Models;
using ReLaitis.UI.Helpers;
using MediaColor = System.Windows.Media.Color;

namespace ReLaitis.UI.Models;

/// <summary>
/// Представление действия в списке цепочки с поддержкой вложенности (индексация, отступы, направляющие ветвления).
/// </summary>
public class ActionItemViewModel
{
    public CommandAction Action { get; }
    public int Index { get; }
    public int Depth { get; }
    public string StepNumber => $"{Index + 1}.";

    public Thickness IndentMargin => new(Math.Max(0, Depth * 24), 0, 0, 4);

    public string BadgeText { get; }
    public SolidColorBrush BadgeBg { get; }
    public SolidColorBrush BadgeFg { get; }
    public SolidColorBrush CardBorderBrush { get; }
    public SolidColorBrush CardBackground { get; }
    public Thickness CardBorderThickness { get; }

    public bool HasGuideLine => Depth > 0;
    public Visibility GuideLineVisibility => HasGuideLine ? Visibility.Visible : Visibility.Collapsed;

    public string DisplayDescription => Action.DisplayDescription;

    public bool IsBlockEnd => Action.Type == ActionType.EndBlock;
    public bool IsElse => Action.Type == ActionType.Else;
    public bool IsBlockOpener => Action.Type is ActionType.IfProcessSelected
        or ActionType.IfProcessExists
        or ActionType.IfVariableValue
        or ActionType.IfWebsiteSelected
        or ActionType.IfWebsiteNavValue
        or ActionType.Loop
        or ActionType.RandomActionBlock;

    public ActionItemViewModel(CommandAction action, int index, int depth)
    {
        Action = action;
        Index = index;
        Depth = Math.Max(0, depth);

        var visuals = ActionVisuals.GetVisuals(action.Type);
        BadgeText = visuals.Label;
        BadgeBg = new SolidColorBrush(visuals.Bg);
        BadgeFg = new SolidColorBrush(visuals.Fg);

        if (IsBlockOpener)
        {
            CardBorderBrush = new SolidColorBrush(visuals.Border);
            CardBackground = new SolidColorBrush(MediaColor.FromRgb(22, 32, 50));
            CardBorderThickness = new Thickness(2, 1, 1, 1);
        }
        else if (IsElse)
        {
            CardBorderBrush = new SolidColorBrush(visuals.Border);
            CardBackground = new SolidColorBrush(MediaColor.FromRgb(28, 25, 23));
            CardBorderThickness = new Thickness(2, 1, 1, 1);
        }
        else if (IsBlockEnd)
        {
            CardBorderBrush = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));
            CardBackground = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));
            CardBorderThickness = new Thickness(1);
        }
        else
        {
            CardBorderBrush = new SolidColorBrush(MediaColor.FromRgb(31, 41, 55));
            CardBackground = new SolidColorBrush(MediaColor.FromRgb(22, 32, 50));
            CardBorderThickness = new Thickness(1);
        }
    }
}
