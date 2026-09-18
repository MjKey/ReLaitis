using ReLaitis.Audio.Feedback;
using ReLaitis.Core.Models;
using Xunit;

namespace ReLaitis.Tests;

public class TtsAndSettingsTests
{
    [Fact]
    public void UserSettings_DefaultValues_ShowHudIsFalseAndEdgeTtsIsDefault()
    {
        var settings = new UserSettings();

        Assert.False(settings.ShowHud, "HUD overlay must be false (disabled) by default");
        Assert.Equal(TtsProviderType.MicrosoftEdgeNeural, settings.TtsProvider);
        Assert.Equal("ru-RU-SvetlanaNeural", settings.EdgeVoiceName);
        Assert.Equal(ListeningMode.Continuous, settings.ListeningMode);
        Assert.True(settings.SoundFeedbackEnabled);
    }

    [Fact]
    public void EdgeNeuralTtsService_BuildSsml_EscapesXmlAndFormatsRate()
    {
        var ssml = EdgeNeuralTtsService.BuildSsml("Тест < & > \" '", "ru-RU-DmitryNeural", 5);

        Assert.Contains("ru-RU-DmitryNeural", ssml);
        Assert.Contains("+50%", ssml);
        Assert.Contains("&lt;", ssml);
        Assert.Contains("&amp;", ssml);
        Assert.Contains("&gt;", ssml);
    }

    [Fact]
    public void TtsManager_AppliesUserSettingsSuccessfully()
    {
        var settings = new UserSettings
        {
            TtsProvider = TtsProviderType.WindowsSapi,
            SpeechRate = -2,
            SapiVoiceName = "Microsoft Irina Desktop"
        };

        using var manager = new TtsManager(settings);
        // Ensure no exceptions thrown on apply
        manager.ApplySettings(settings);
    }

    [Fact]
    public void RussianPluralizer_FormatsCorrectForms()
    {
        Assert.Equal("1 фраза", ReLaitis.Core.Helpers.RussianPluralizer.Format(1, "фраза", "фразы", "фраз"));
        Assert.Equal("2 фразы", ReLaitis.Core.Helpers.RussianPluralizer.Format(2, "фраза", "фразы", "фраз"));
        Assert.Equal("5 фраз", ReLaitis.Core.Helpers.RussianPluralizer.Format(5, "фраза", "фразы", "фраз"));
        Assert.Equal("11 фраз", ReLaitis.Core.Helpers.RussianPluralizer.Format(11, "фраза", "фразы", "фраз"));
        Assert.Equal("21 фраза", ReLaitis.Core.Helpers.RussianPluralizer.Format(21, "фраза", "фразы", "фраз"));
        Assert.Equal("62 команды", ReLaitis.Core.Helpers.RussianPluralizer.Format(62, "команда", "команды", "команд"));
    }

    [Fact]
    public void VoiceCommand_DisplaySubtitle_PluralizesCorrectly()
    {
        var cmd = new VoiceCommand
        {
            Name = "тест",
            Phrases = ["раз", "два", "три", "четыре", "пять"],
            Actions = [new CommandAction { Type = ReLaitis.Core.Enums.ActionType.OpenFile, Parameters = ["calc.exe"] }]
        };

        Assert.Equal("раз, два, три, четыре, пять • Запуск: calc.exe", cmd.DisplaySubtitle);
    }
}
