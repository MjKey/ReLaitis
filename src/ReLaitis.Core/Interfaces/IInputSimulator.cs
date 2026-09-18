using ReLaitis.Core.Enums;

namespace ReLaitis.Core.Interfaces;

/// <summary>
/// Интерфейс эмулятора аппаратного ввода (клавиатура, мышь).
/// </summary>
public interface IInputSimulator
{
    void SendHotkey(string keyCombination, ButtonAction action = ButtonAction.Press);
    void TypeText(string text);
    void MoveMouse(int x, int y, MouseMoveType moveType, string? scribbleCoords = null);
    void MouseClick(int button, ButtonAction action = ButtonAction.Press);
    void MouseScroll(MouseScrollType scrollType, int delta);
    (int X, int Y) GetMousePosition();
}
