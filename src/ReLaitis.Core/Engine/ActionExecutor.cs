using System.Diagnostics;
using System.Text.RegularExpressions;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Models;

namespace ReLaitis.Core.Engine;

/// <summary>
/// Исполнитель цепочки действий макроса с поддержкой условий, циклов, переменных и отмены.
/// </summary>
public class ActionExecutor
{
    public async Task ExecuteAsync(IReadOnlyList<CommandAction> actions, MacroExecutionContext context)
    {
        if (actions == null || actions.Count == 0)
            return;

        await ExecuteBlockAsync(actions, 0, actions.Count, context);
    }

    private async Task<int> ExecuteBlockAsync(
        IReadOnlyList<CommandAction> actions,
        int startIndex,
        int endIndex,
        MacroExecutionContext context)
    {
        var i = startIndex;

        while (i < endIndex)
        {
            if (context.CancellationToken.IsCancellationRequested || context.ShouldBreak)
                break;

            var action = actions[i];

            switch (action.Type)
            {
                case ActionType.OpenFile:
                    ExecuteOpenFile(action, context);
                    i++;
                    break;

                case ActionType.CloseApp:
                    ExecuteCloseApp(action, context);
                    i++;
                    break;

                case ActionType.ShowWindow:
                    ExecuteShowWindow(action, context);
                    i++;
                    break;

                case ActionType.Hotkeys:
                    ExecuteHotkeys(action, context);
                    i++;
                    break;

                case ActionType.TypeText:
                    ExecuteTypeText(action, context);
                    i++;
                    break;

                case ActionType.MouseMove:
                    ExecuteMouseMove(action, context);
                    i++;
                    break;

                case ActionType.MouseButton:
                    ExecuteMouseButton(action, context);
                    i++;
                    break;

                case ActionType.MouseScroll:
                    ExecuteMouseScroll(action, context);
                    i++;
                    break;

                case ActionType.Say:
                    await ExecuteSayAsync(action, context);
                    i++;
                    break;

                case ActionType.PlayAudio:
                    await ExecutePlayAudioAsync(action, context);
                    i++;
                    break;

                case ActionType.Pause:
                    await ExecutePauseAsync(action, context);
                    i++;
                    break;

                case ActionType.SetVariableValue:
                    ExecuteSetVariable(action, context);
                    i++;
                    break;

                case ActionType.Notify:
                    ExecuteNotify(action, context);
                    i++;
                    break;

                case ActionType.VoiceCommand:
                    ExecuteVoiceCommand(action, context);
                    i++;
                    break;

                case ActionType.BatchScript:
                    ExecuteBatchScript(action, context);
                    i++;
                    break;

                case ActionType.CSharpScript:
                    await ExecuteCSharpScriptAsync(action, context);
                    i++;
                    break;

                case ActionType.OpenURL:
                    ExecuteOpenUrl(action, context);
                    i++;
                    break;

                case ActionType.HttpWebRequest:
                    await ExecuteHttpWebRequestAsync(action, context);
                    i++;
                    break;

                case ActionType.TogglePackActivity:
                    ExecuteTogglePackActivity(action, context);
                    i++;
                    break;

                case ActionType.ScheduleEvent:
                    ExecuteScheduleEvent(action, context);
                    i++;
                    break;

                case ActionType.WaitNextPhrase: // also handles WaitNextCommand (alias 36)
                    await ExecuteWaitNextPhraseAsync(action, context);
                    i++;
                    break;

                case ActionType.GetUrlSelectorText:
                    await ExecuteGetUrlSelectorTextAsync(action, context);
                    i++;
                    break;

                case ActionType.JetAim:
                    ExecuteJetAim(action, context);
                    i++;
                    break;

                case ActionType.MouseMoveOn:
                    ExecuteMouseMoveOn(action, context);
                    i++;
                    break;

                case ActionType.WebPageClick:
                case ActionType.WebPageNavClick:
                    await ExecuteWebPageClickAsync(action, context);
                    i++;
                    break;

                case ActionType.WebPageNavigate:
                    await ExecuteWebPageNavigateAsync(action, context);
                    i++;
                    break;

                case ActionType.WebPageScript:
                    await ExecuteWebPageScriptAsync(action, context);
                    i++;
                    break;

                case ActionType.WebPageFocus:
                    await ExecuteWebPageFocusAsync(action, context);
                    i++;
                    break;

                case ActionType.WebPageGetText:
                    await ExecuteWebPageGetTextAsync(action, context);
                    i++;
                    break;

                case ActionType.WebPagePopupOpen:
                    await ExecuteWebPageTabActionAsync(action, context);
                    i++;
                    break;

                case ActionType.IfProcessSelected:
                case ActionType.IfProcessExists:
                case ActionType.IfVariableValue:
                case ActionType.IfWebsiteSelected:
                    i = await HandleConditionalAsync(actions, i, endIndex, context);
                    break;

                case ActionType.Loop:
                    i = await HandleLoopAsync(actions, i, endIndex, context);
                    break;

                case ActionType.While:
                    i = await HandleWhileAsync(actions, i, endIndex, context);
                    break;

                case ActionType.RandomActionBlock:
                    i = await HandleRandomBlockAsync(actions, i, endIndex, context);
                    break;

                case ActionType.Break:
                    context.ShouldBreak = true;
                    i++;
                    break;

                case ActionType.Else:
                case ActionType.EndBlock:
                case ActionType.Comment:
                default:
                    i++;
                    break;
            }
        }

        return i;
    }

    #region Action Handlers

    private static void ExecuteOpenFile(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var filePath = context.ResolveVariables(action.Parameters[0]);
        var args = action.Parameters.Length > 1 ? context.ResolveVariables(action.Parameters[1]) : "";
        context.Windows.StartProcess(filePath, args);
    }

    private static void ExecuteCloseApp(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var processName = context.ResolveVariables(action.Parameters[0]);
        var behaviour = CloseAppBehaviour.Close;
        if (action.Parameters.Length > 1 && int.TryParse(action.Parameters[1], out var bInt))
            behaviour = (CloseAppBehaviour)bInt;
        context.Windows.CloseProcess(processName, behaviour);
    }

    private static void ExecuteShowWindow(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var processName = context.ResolveVariables(action.Parameters[0]);
        var command = ShowWindowCommandType.Normal;
        if (action.Parameters.Length > 1 && int.TryParse(action.Parameters[1], out var cInt))
            command = (ShowWindowCommandType)cInt;
        context.Windows.SetWindowState(processName, command);
    }

    private static void ExecuteHotkeys(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var hotkey = context.ResolveVariables(action.Parameters[0]);
        var buttonAction = ButtonAction.Press;
        if (action.Parameters.Length > 1 && int.TryParse(action.Parameters[1], out var aInt))
            buttonAction = (ButtonAction)aInt;
        context.Input.SendHotkey(hotkey, buttonAction);
    }

    private static void ExecuteTypeText(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var text = context.ResolveVariables(action.Parameters[0]);
        context.Input.TypeText(text);
    }

    private static void ExecuteMouseMove(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length < 2) return;
        var xStr = context.ResolveVariables(action.Parameters[0]);
        var yStr = context.ResolveVariables(action.Parameters[1]);
        if (!double.TryParse(xStr, System.Globalization.CultureInfo.InvariantCulture, out var xVal) || 
            !double.TryParse(yStr, System.Globalization.CultureInfo.InvariantCulture, out var yVal)) 
            return;

        var moveType = MouseMoveType.Point;
        if (action.Parameters.Length > 2 && int.TryParse(action.Parameters[2], out var mtInt))
            moveType = (MouseMoveType)mtInt;

        int finalX = (int)xVal;
        int finalY = (int)yVal;
        string? scribble = null;

        if (action.Parameters.Length > 3)
        {
            var p3 = action.Parameters[3].Trim();
            if (p3 == "1" || p3.Equals("Percent", StringComparison.OrdinalIgnoreCase))
            {
                var sw = context.ScreenWidth > 0 ? context.ScreenWidth : 1920;
                var sh = context.ScreenHeight > 0 ? context.ScreenHeight : 1080;
                finalX = (int)(sw * xVal / 100.0);
                finalY = (int)(sh * yVal / 100.0);
            }
            else if (moveType == MouseMoveType.Scribble)
            {
                scribble = p3;
            }
        }

        context.Input.MoveMouse(finalX, finalY, moveType, scribble);
    }

    private static void ExecuteMouseButton(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        if (!int.TryParse(action.Parameters[0], out var button))
            button = 0; // Left button by default

        var btnAction = ButtonAction.Press;
        if (action.Parameters.Length > 1 && int.TryParse(action.Parameters[1], out var baInt))
            btnAction = (ButtonAction)baInt;

        context.Input.MouseClick(button, btnAction);
    }

    private static void ExecuteMouseScroll(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var scrollType = MouseScrollType.Vertical;
        if (int.TryParse(action.Parameters[0], out var stInt))
            scrollType = (MouseScrollType)stInt;

        var delta = 120;
        if (action.Parameters.Length > 1 && int.TryParse(context.ResolveVariables(action.Parameters[1]), out var d))
            delta = d;

        context.Input.MouseScroll(scrollType, delta);
    }

    private static async Task ExecuteSayAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var phrase = context.ResolveVariables(action.Parameters[0]);
        await context.Voice.SpeakAsync(phrase, context.CancellationToken);
    }

    private static async Task ExecutePlayAudioAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var path = context.ResolveVariables(action.Parameters[0]);
        await context.Voice.PlayAudioAsync(path, context.CancellationToken);
    }

    private static async Task ExecutePauseAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        if (!int.TryParse(context.ResolveVariables(action.Parameters[0]), out var ms))
            ms = 100;

        var pauseType = PauseType.Static;
        if (action.Parameters.Length > 1 && int.TryParse(action.Parameters[1], out var ptInt))
            pauseType = (PauseType)ptInt;

        if (pauseType == PauseType.Random)
            ms = Random.Shared.Next(0, ms + 1);

        if (ms > 0)
            await Task.Delay(ms, context.CancellationToken);
    }

    private static void ExecuteSetVariable(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length < 2) return;
        var varName = action.Parameters[0];

        var op = ArithmeticOperation.None;
        if (action.Parameters.Length > 2 && int.TryParse(action.Parameters[2], out var opInt))
        {
            if (opInt != 15 && Enum.IsDefined(typeof(ArithmeticOperation), opInt))
                op = (ArithmeticOperation)opInt;
        }

        if (op == ArithmeticOperation.Replace)
        {
            // В Laitis при операции Replace (13):
            // Parameters[0] = имя переменной
            // Parameters[1] = искомый текст (что заменять, напр. "плюс")
            // Parameters[3] = строка замены (на что заменять, напр. "+")
            var currentVal = context.GetVariable(varName) ?? "";
            var find = context.ResolveVariables(action.Parameters[1]);
            var replace = action.Parameters.Length > 3 ? context.ResolveVariables(action.Parameters[3]) : "";

            var result = string.IsNullOrEmpty(currentVal) || string.IsNullOrEmpty(find)
                ? currentVal
                : Regex.Replace(currentVal, Regex.Escape(find), replace, RegexOptions.IgnoreCase);

            context.SetVariable(varName, result);
            return;
        }

        var operand1 = context.ResolveVariables(action.Parameters[1]);
        var operand2 = "";
        if (action.Parameters.Length > 3)
            operand2 = context.ResolveVariables(action.Parameters[3]);

        string calcResult;
        if (op == ArithmeticOperation.None && string.IsNullOrEmpty(operand2))
        {
            calcResult = VariableCalculator.EvaluateExpression(operand1);
        }
        else
        {
            calcResult = VariableCalculator.Calculate(operand1, op, operand2);
        }

        context.SetVariable(varName, calcResult);
    }

    private static void ExecuteNotify(CommandAction action, MacroExecutionContext context)
    {
        var text = action.Parameters.Length > 1 ? context.ResolveVariables(action.Parameters[1]) : "";
        context.TriggerNotification("ReLaitis", text);
    }

    private static void ExecuteVoiceCommand(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var commandPhrase = context.ResolveVariables(action.Parameters[0]);
        context.RequestVoiceCommand(commandPhrase);
    }

    private static void ExecuteBatchScript(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var script = context.ResolveVariables(action.Parameters[0]);
        var tempBat = Path.Combine(Path.GetTempPath(), $"relaitis_cmd_{Guid.NewGuid():N}.bat");
        try
        {
            var content = $"@chcp 65001 >nul\r\n{script}\r\n";
            File.WriteAllText(tempBat, content, System.Text.Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{tempBat}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(30000);
        }
        catch
        {
        }
        finally
        {
            try
            {
                if (File.Exists(tempBat))
                    File.Delete(tempBat);
            }
            catch { }
        }
    }

    private static void ExecuteMouseMoveOn(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;

        // В Laitis: Parameters[1] = имя элемента (напр. "{название}"), Parameters[0] может быть пустым
        string elementName;
        string actionName = "";

        if (action.Parameters.Length > 1 && !string.IsNullOrWhiteSpace(action.Parameters[1]))
        {
            elementName = context.ResolveVariables(action.Parameters[1]);
            actionName = context.ResolveVariables(action.Parameters[0]);
        }
        else
        {
            elementName = context.ResolveVariables(action.Parameters[0]);
        }

        if (!string.IsNullOrWhiteSpace(elementName))
        {
            context.Windows.FindAndClickElementByName(elementName, actionName);
        }
    }

    private static readonly HttpClient HttpClientInstance = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static void ExecuteOpenUrl(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var url = context.ResolveVariables(action.Parameters[0]).Trim();
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Игнорируем ошибки вызова браузера
        }
    }

    private static async Task ExecuteHttpWebRequestAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;

        string method;
        string url;
        string? responseVar = null;
        string? body = null;

        if (action.Parameters.Length >= 2 &&
            (action.Parameters[0].StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             action.Parameters[0].StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            // ReLaitis legacy fallback: P[0] = URL, P[1] = Method, P[2] = Body, P[3] = ResponseVar
            url = context.ResolveVariables(action.Parameters[0]).Trim();
            method = context.ResolveVariables(action.Parameters[1]).Trim().ToUpperInvariant();
            body = action.Parameters.Length > 2 ? context.ResolveVariables(action.Parameters[2]) : null;
            responseVar = action.Parameters.Length > 3 ? action.Parameters[3].Trim() : null;
        }
        else
        {
            // Canonical Laitis: P[0] = Method (0=GET, 1=POST, 2=PUT, 3=DELETE, 4=PATCH or name), P[1] = URL, P[2] = ResponseVar
            var rawMethod = context.ResolveVariables(action.Parameters[0]).Trim();
            method = rawMethod switch
            {
                "0" => "GET",
                "1" => "POST",
                "2" => "PUT",
                "3" => "DELETE",
                "4" => "PATCH",
                _ => string.IsNullOrEmpty(rawMethod) ? "GET" : rawMethod.ToUpperInvariant()
            };
            url = action.Parameters.Length > 1 ? context.ResolveVariables(action.Parameters[1]).Trim() : "";
            responseVar = action.Parameters.Length > 2 ? action.Parameters[2].Trim() : null;
            if (action.Parameters.Length > 3)
            {
                body = context.ResolveVariables(action.Parameters[3]);
            }
        }

        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (!string.IsNullOrEmpty(body) && method is "POST" or "PUT" or "PATCH")
            {
                request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            }

            using var response = await HttpClientInstance.SendAsync(request, context.CancellationToken);
            if (!string.IsNullOrEmpty(responseVar))
            {
                var content = await response.Content.ReadAsStringAsync(context.CancellationToken);
                context.Variables[responseVar] = content;
            }
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(responseVar))
                context.Variables[responseVar] = $"Error: {ex.Message}";
        }
    }

    private static async Task ExecuteGetUrlSelectorTextAsync(CommandAction action, MacroExecutionContext context)
    {
        // Canonical Laitis: P[0]=Method, P[1]=URL, P[2]=Selector, P[3]=TargetVar
        if (action.Parameters.Length < 2) return;
        var rawMethod = action.Parameters[0].Trim();
        var method = rawMethod switch
        {
            "0" or "" => "GET",
            "1" => "POST",
            _ => rawMethod.ToUpperInvariant()
        };

        var url = context.ResolveVariables(action.Parameters[1]).Trim();
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        var selector = action.Parameters.Length > 2 ? context.ResolveVariables(action.Parameters[2]).Trim() : "";
        var targetVar = action.Parameters.Length > 3 ? action.Parameters[3].Trim() : "Result";

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            using var response = await HttpClientInstance.SendAsync(request, context.CancellationToken);
            var html = await response.Content.ReadAsStringAsync(context.CancellationToken);

            var extracted = ExtractSelectorText(html, selector);
            context.SetVariable(targetVar, extracted);
        }
        catch (Exception ex)
        {
            context.SetVariable(targetVar, $"Error: {ex.Message}");
        }
    }

    private static string ExtractSelectorText(string html, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return html;

        try
        {
            if (selector.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            {
                var pattern = selector["regex:".Length..].Trim();
                var m = Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (m.Success)
                    return m.Groups.Count > 1 ? m.Groups[1].Value : m.Value;
            }

            var cleanSelector = selector.Trim();
            if (cleanSelector.StartsWith('#'))
            {
                var id = cleanSelector[1..];
                var m = Regex.Match(html, $@"id\s*=\s*[""']{Regex.Escape(id)}[""'][^>]*>(?<content>.*?)</", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (m.Success) return StripHtmlTags(m.Groups["content"].Value).Trim();
            }
            else if (cleanSelector.StartsWith('.'))
            {
                var cls = cleanSelector[1..];
                var m = Regex.Match(html, $@"class\s*=\s*[""'][^""']*{Regex.Escape(cls)}[^""']*[""'][^>]*>(?<content>.*?)</", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (m.Success) return StripHtmlTags(m.Groups["content"].Value).Trim();
            }
            else
            {
                var m = Regex.Match(html, $@"<{Regex.Escape(cleanSelector)}[^>]*>(?<content>.*?)</{Regex.Escape(cleanSelector)}>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (m.Success) return StripHtmlTags(m.Groups["content"].Value).Trim();
            }

            var fallbackMatch = Regex.Match(html, selector, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (fallbackMatch.Success)
            {
                return fallbackMatch.Groups.Count > 1 ? fallbackMatch.Groups[1].Value : fallbackMatch.Value;
            }
        }
        catch
        {
        }

        return html;
    }

    private static string StripHtmlTags(string input)
    {
        return Regex.Replace(input, "<.*?>", string.Empty);
    }

    private static void ExecuteTogglePackActivity(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;
        var packName = context.ResolveVariables(action.Parameters[0]);
        var state = 0; // Default: Toggle (0: Toggle, 1: Enable, 2: Disable)
        if (action.Parameters.Length > 1 && int.TryParse(action.Parameters[1], out var s))
            state = s;

        context.RequestTogglePack(packName, state);
    }

    private static void ExecuteScheduleEvent(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length < 2) return;

        if (action.Parameters.Length >= 3)
        {
            // Canonical Laitis: P[0] = ScheduleType, P[1] = Time/Delay, P[2] = Phrase
            var timeStr = context.ResolveVariables(action.Parameters[1]);
            if (int.TryParse(timeStr, out var delayMs))
            {
                var phrase = context.ResolveVariables(action.Parameters[2]);
                context.RequestScheduleEvent(Math.Max(0, delayMs), phrase);
            }
        }
        else
        {
            // 2-param format: P[0] = Delay, P[1] = Phrase
            var delayStr = context.ResolveVariables(action.Parameters[0]);
            if (int.TryParse(delayStr, out var delayMs))
            {
                var phrase = context.ResolveVariables(action.Parameters[1]);
                context.RequestScheduleEvent(Math.Max(0, delayMs), phrase);
            }
        }
    }

    private static async Task ExecuteWaitNextPhraseAsync(CommandAction action, MacroExecutionContext context)
    {
        if (context.WaitForNextPhraseAsync == null) return;
        var timeoutMs = 5000;
        if (action.Parameters.Length > 1 && int.TryParse(context.ResolveVariables(action.Parameters[1]), out var t))
        {
            timeoutMs = t == 0 ? Timeout.Infinite : Math.Max(1000, t);
        }

        var resultPhrase = await context.WaitForNextPhraseAsync(timeoutMs);

        if (action.Parameters.Length > 0 && !string.IsNullOrWhiteSpace(action.Parameters[0]))
        {
            var targetVar = action.Parameters[0].Trim();
            context.SetVariable(targetVar, resultPhrase ?? string.Empty);
        }
    }

    private static void ExecuteJetAim(CommandAction action, MacroExecutionContext context)
    {
        context.RequestJetAim();
    }

    private static async Task ExecuteWebPageClickAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0 || context.BrowserBridge == null) return;
        var target = context.ResolveVariables(action.Parameters[0]).Trim();
        if (!string.IsNullOrEmpty(target))
        {
            await context.BrowserBridge.ClickElementAsync(target, context.CancellationToken);
        }
    }

    private static async Task ExecuteWebPageNavigateAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0 || context.BrowserBridge == null) return;
        var url = context.ResolveVariables(action.Parameters[0]).Trim();
        if (!string.IsNullOrEmpty(url))
        {
            await context.BrowserBridge.NavigateAsync(url, context.CancellationToken);
        }
    }

    private static async Task ExecuteWebPageScriptAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0 || context.BrowserBridge == null) return;
        var script = context.ResolveVariables(action.Parameters[0]);
        if (!string.IsNullOrEmpty(script))
        {
            if (string.Equals(script.Trim(), "hints", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(script.Trim(), "toggle_hints", StringComparison.OrdinalIgnoreCase))
            {
                await context.BrowserBridge.ToggleHintsAsync(true, context.CancellationToken);
            }
            else if (string.Equals(script.Trim(), "hide_hints", StringComparison.OrdinalIgnoreCase))
            {
                await context.BrowserBridge.ToggleHintsAsync(false, context.CancellationToken);
            }
            else
            {
                await context.BrowserBridge.ExecuteScriptAsync(script, context.CancellationToken);
            }
        }
    }

    private static async Task ExecuteWebPageFocusAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0 || context.BrowserBridge == null) return;
        var selector = context.ResolveVariables(action.Parameters[0]).Trim();
        if (!string.IsNullOrEmpty(selector))
        {
            var script = $"document.querySelector('{selector.Replace("'", "\\'")}')?.focus();";
            await context.BrowserBridge.ExecuteScriptAsync(script, context.CancellationToken);
        }
    }

    private static async Task ExecuteWebPageGetTextAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0 || context.BrowserBridge == null) return;
        var selector = context.ResolveVariables(action.Parameters[0]).Trim();
        var targetVar = action.Parameters.Length > 1 ? action.Parameters[1].Trim() : "WebText";

        var text = await context.BrowserBridge.GetElementTextAsync(selector, context.CancellationToken);
        if (text != null)
        {
            context.SetVariable(targetVar, text);
        }
    }

    private static async Task ExecuteWebPageTabActionAsync(CommandAction action, MacroExecutionContext context)
    {
        if (context.BrowserBridge == null) return;
        var cmd = action.Parameters.Length > 0 ? context.ResolveVariables(action.Parameters[0]).Trim() : "new";
        await context.BrowserBridge.TabActionAsync(cmd, context.CancellationToken);
    }

    #endregion

    #region Flow Control (Conditions, Loops, Blocks)

    private async Task<int> HandleConditionalAsync(
        IReadOnlyList<CommandAction> actions,
        int currentIndex,
        int endIndex,
        MacroExecutionContext context)
    {
        var conditionAction = actions[currentIndex];
        var conditionResult = EvaluateCondition(conditionAction, context);

        var (elseIndex, endBlockIndex) = FindMatchingBranching(actions, currentIndex + 1, endIndex);

        if (conditionResult)
        {
            var blockEnd = elseIndex != -1 ? elseIndex : endBlockIndex;
            await ExecuteBlockAsync(actions, currentIndex + 1, blockEnd, context);
        }
        else if (elseIndex != -1)
        {
            await ExecuteBlockAsync(actions, elseIndex + 1, endBlockIndex, context);
        }

        return endBlockIndex < endIndex ? endBlockIndex + 1 : endIndex;
    }

    private async Task<int> HandleLoopAsync(
        IReadOnlyList<CommandAction> actions,
        int currentIndex,
        int endIndex,
        MacroExecutionContext context)
    {
        var loopAction = actions[currentIndex];
        var count = 1;
        if (loopAction.Parameters.Length > 0 && int.TryParse(context.ResolveVariables(loopAction.Parameters[0]), out var c))
            count = Math.Max(0, c);

        var (_, endBlockIndex) = FindMatchingBranching(actions, currentIndex + 1, endIndex);

        for (var step = 0; step < count; step++)
        {
            if (context.CancellationToken.IsCancellationRequested || context.ShouldBreak)
                break;

            await ExecuteBlockAsync(actions, currentIndex + 1, endBlockIndex, context);
        }

        context.ShouldBreak = false;
        return endBlockIndex < endIndex ? endBlockIndex + 1 : endIndex;
    }

    private async Task<int> HandleWhileAsync(
        IReadOnlyList<CommandAction> actions,
        int currentIndex,
        int endIndex,
        MacroExecutionContext context)
    {
        var whileAction = actions[currentIndex];
        var (_, endBlockIndex) = FindMatchingBranching(actions, currentIndex + 1, endIndex);

        while (!context.CancellationToken.IsCancellationRequested && !context.ShouldBreak && EvaluateCondition(whileAction, context))
        {
            await ExecuteBlockAsync(actions, currentIndex + 1, endBlockIndex, context);
        }

        context.ShouldBreak = false;
        return endBlockIndex < endIndex ? endBlockIndex + 1 : endIndex;
    }

    private async Task<int> HandleRandomBlockAsync(
        IReadOnlyList<CommandAction> actions,
        int currentIndex,
        int endIndex,
        MacroExecutionContext context)
    {
        var (_, endBlockIndex) = FindMatchingBranching(actions, currentIndex + 1, endIndex);
        var subActionsCount = endBlockIndex - (currentIndex + 1);

        if (subActionsCount > 0)
        {
            var pickedIndex = currentIndex + 1 + Random.Shared.Next(0, subActionsCount);
            await ExecuteBlockAsync(actions, pickedIndex, pickedIndex + 1, context);
        }

        return endBlockIndex < endIndex ? endBlockIndex + 1 : endIndex;
    }

    private static bool EvaluateCondition(CommandAction action, MacroExecutionContext context)
    {
        switch (action.Type)
        {
            case ActionType.IfProcessSelected:
                if (action.Parameters.Length == 0) return false;
                var targetProc = context.ResolveVariables(action.Parameters[0]);
                var procs = targetProc.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return procs.Any(p => context.Windows.IsProcessActive(p));

            case ActionType.IfProcessExists:
                if (action.Parameters.Length == 0) return false;
                var proc = context.ResolveVariables(action.Parameters[0]);
                var procs2 = proc.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return procs2.Any(p => context.Windows.IsProcessRunning(p));

            case ActionType.While:
            case ActionType.IfVariableValue:
                if (action.Parameters.Length < 3) return false;
                var varName = action.Parameters[0];
                var actualValue = context.Variables.TryGetValue(varName, out var val) ? val : "";
                var op = VariableOperator.Equals;
                if (int.TryParse(action.Parameters[1], out var opInt))
                    op = (VariableOperator)opInt;
                var expectedValue = context.ResolveVariables(action.Parameters[2]);
                return VariableCalculator.EvaluateCondition(actualValue, op, expectedValue);

            case ActionType.IfWebsiteSelected:
                if (action.Parameters.Length == 0) return false;
                var expectedPart = context.ResolveVariables(action.Parameters[0]);
                if (context.BrowserBridge?.IsConnected == true)
                {
                    var bUrl = context.BrowserBridge.CurrentUrl ?? "";
                    var bTitle = context.BrowserBridge.CurrentTitle ?? "";
                    if (bUrl.Contains(expectedPart, StringComparison.OrdinalIgnoreCase) ||
                        bTitle.Contains(expectedPart, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                var title = context.Windows.GetActiveWindowTitle();
                return title.Contains(expectedPart, StringComparison.OrdinalIgnoreCase);

            default:
                return true;
        }
    }

    private static (int ElseIndex, int EndBlockIndex) FindMatchingBranching(
        IReadOnlyList<CommandAction> actions,
        int startIndex,
        int endIndex)
    {
        var depth = 0;
        var elseIndex = -1;

        for (var i = startIndex; i < endIndex; i++)
        {
            var actionType = actions[i].Type;

            if (IsBlockOpener(actionType))
            {
                depth++;
            }
            else if (actionType == ActionType.Else && depth == 0)
            {
                elseIndex = i;
            }
            else if (actionType == ActionType.EndBlock)
            {
                if (depth == 0)
                    return (elseIndex, i);
                depth--;
            }
        }

        return (elseIndex, endIndex);
    }

    private static bool IsBlockOpener(ActionType type) =>
        type is ActionType.IfProcessSelected
             or ActionType.IfProcessExists
             or ActionType.IfVariableValue
             or ActionType.IfWebsiteSelected
             or ActionType.IfWebsiteNavValue
             or ActionType.Loop
             or ActionType.While
             or ActionType.RandomActionBlock;

    private static async Task ExecuteCSharpScriptAsync(CommandAction action, MacroExecutionContext context)
    {
        if (action.Parameters.Length == 0) return;

        var rawCode = action.Parameters[0];
        if (string.IsNullOrWhiteSpace(rawCode)) return;

        var targetVar = action.Parameters.Length > 1 ? action.Parameters[1].Trim() : null;

        try
        {
            var globals = new CSharpScriptGlobals(context);
            var result = await CSharpScriptEngine.ExecuteAsync(rawCode, globals, context.CancellationToken);

            if (!string.IsNullOrEmpty(targetVar) && result != null)
            {
                context.SetVariable(targetVar, result.ToString() ?? string.Empty);
            }
        }
        catch (Microsoft.CodeAnalysis.Scripting.CompilationErrorException ex)
        {
            var errorDetails = string.Join("; ", ex.Diagnostics.Select(d => d.GetMessage()));
            context.TriggerNotification("Ошибка C# скрипта", errorDetails);
        }
        catch (Exception ex)
        {
            context.TriggerNotification("Ошибка выполнения C#", ex.Message);
        }
    }

    #endregion
}
