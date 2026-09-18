namespace ReLaitis.Core.Enums;

public enum CloseAppBehaviour
{
    Close = 0,
    Kill = 1
}

public enum ButtonAction
{
    Press = 0,
    Down = 1,
    Up = 2
}

public enum MouseMoveType
{
    Point = 0,
    Relative = 1,
    Scribble = 2
}

public enum MouseScrollType
{
    Vertical = 0,
    Horizontal = 1
}

public enum PauseType
{
    Static = 0,
    Random = 1
}

public enum ArithmeticOperation
{
    None = 0,
    Add = 1,
    Subtract = 2,
    Multiply = 3,
    Divide = 4,
    Modulo = 5,
    Power = 6,
    Sqrt = 7,
    Round = 8,
    Floor = 9,
    Ceil = 10,
    Abs = 11,
    Random = 12,
    Replace = 13,
    Substring = 14
}

public enum VariableOperator
{
    Equals = 0,
    NotEquals = 1,
    Greater = 2,
    Less = 3,
    GreaterOrEqual = 4,
    LessOrEqual = 5,
    Contains = 6,
    NotContains = 7,
    StartsWith = 8,
    EndsWith = 9
}

public enum ShowWindowCommandType
{
    Normal = 0,
    Minimize = 1,
    Maximize = 2,
    Restore = 3,
    Show = 4
}

public enum StateSwitchingAction
{
    Toggle = 0,
    Enable = 1,
    Disable = 2
}

public enum MouseMoveTranslation
{
    Pixels = 0,
    Percent = 1
}

public enum ScheduleEventType
{
    Timer = 0,
    Delay = 0,
    DateTime = 1,
    ExactTime = 1,
    Interval = 2
}

public enum NotificationType
{
    Information = 0,
    Warning = 1,
    Error = 2
}

public enum HttpWebRequestMethod
{
    Get = 0,
    GET = 0,
    Post = 1,
    POST = 1,
    Put = 2,
    Delete = 3,
    Patch = 4
}

public enum WebPageNavigateAction
{
    Focus = 0,
    Click = 1,
    Next = 2,
    Previous = 3
}
