using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KeyAsio.Configuration;
using KeyAsio.Configuration.Serialization;
using KeyAsio.Core.Audio;
using KeyAsio.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Milki.Extensions.Configuration;
using Moq;
using SukiUI.Toasts;

namespace KeyAsio.UnitTests;

public sealed class ChordSettingsTests
{
    [AvaloniaFact]
    public async Task ToggleIsDefaultOffAndSettingsManagerPersistsBothValues()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var converter = MyYamlConfigurationConverter.Instance;
            var settings = ConfigurationFactory.GetConfiguration<AppSettings>(folder, "appsettings.yaml", converter);
            Assert.False(settings.Sync.Playback.WaitForFinalChordKey);
            Assert.False(settings.Sync.Playback.OnlyPlayMatchingNotes);
            using var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, settings,
                Mock.Of<IPlaybackEngine>(), Mock.Of<ISukiToastManager>());
            foreach (var (grouped, filtered) in new[] { (true, false), (true, true), (false, true), (false, false) })
            {
                settings.Sync.Playback.WaitForFinalChordKey = grouped;
                settings.Sync.Playback.OnlyPlayMatchingNotes = filtered;
                await Task.Delay(750);
                Dispatcher.UIThread.RunJobs();
                var text = await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml"));
                var reloaded = (AppSettings)converter.DeserializeSettings(text, typeof(AppSettings));
                Assert.Equal(grouped, reloaded.Sync.Playback.WaitForFinalChordKey);
                Assert.Equal(filtered, reloaded.Sync.Playback.OnlyPlayMatchingNotes);
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void ExistingChordConfigurationDoesNotOptIntoNoteOnlyFiltering()
    {
        var restored = (AppSettings)MyYamlConfigurationConverter.Instance.DeserializeSettings(
            "sync:\n  playback:\n    waitForFinalChordKey: true\n", typeof(AppSettings));
        Assert.True(restored.Sync.Playback.WaitForFinalChordKey);
        Assert.False(restored.Sync.Playback.OnlyPlayMatchingNotes);
    }
}
