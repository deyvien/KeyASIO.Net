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
    public async Task FallbackToggleIsDefaultOffAndPersists()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var converter = MyYamlConfigurationConverter.Instance;
            var settings = ConfigurationFactory.GetConfiguration<AppSettings>(folder, "appsettings.yaml", converter);
            Assert.False(settings.Sync.Playback.UseFallbackHitsounds);
            using var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, settings,
                Mock.Of<IPlaybackEngine>(), Mock.Of<ISukiToastManager>());
            foreach (bool enabled in new[] { true, false })
            {
                settings.Sync.Playback.UseFallbackHitsounds = enabled;
                await Task.Delay(750);
                Dispatcher.UIThread.RunJobs();
                var saved = (AppSettings)converter.DeserializeSettings(await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml")), typeof(AppSettings));
                Assert.Equal(enabled, saved.Sync.Playback.UseFallbackHitsounds);
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    [AvaloniaFact]
    public async Task ChordLevelAndTogglePersistAndApplyIndependently()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var converter = MyYamlConfigurationConverter.Instance;
            var settings = ConfigurationFactory.GetConfiguration<AppSettings>(folder, "appsettings.yaml", converter);
            Assert.False(settings.Sync.Playback.UseSeparateChordVolume);
            Assert.Equal(100, settings.Audio.ChordVolume);
            var engine = new Mock<IPlaybackEngine>();
            engine.SetupAllProperties();
            using var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, settings,
                engine.Object, Mock.Of<ISukiToastManager>());
            settings.Audio.EffectVolume = 20;
            settings.Audio.ChordVolume = 70;
            Assert.Equal(.2f, engine.Object.ChordVolume);
            settings.Sync.Playback.UseSeparateChordVolume = true;
            Assert.Equal(.7f, engine.Object.ChordVolume);
            settings.Audio.EffectVolume = 0;
            Assert.Equal(0, engine.Object.EffectVolume);
            Assert.Equal(.7f, engine.Object.ChordVolume);
            await Task.Delay(750);
            Dispatcher.UIThread.RunJobs();
            var saved = (AppSettings)converter.DeserializeSettings(await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml")), typeof(AppSettings));
            Assert.True(saved.Sync.Playback.UseSeparateChordVolume);
            Assert.Equal(70, saved.Audio.ChordVolume);
            settings.Sync.Playback.UseSeparateChordVolume = false;
            Assert.Equal(0, engine.Object.ChordVolume);
            Assert.Equal(70, settings.Audio.ChordVolume); // Turning off does not discard the saved level.
            settings.Audio.EffectVolume = 40;
            Assert.Equal(.4f, engine.Object.ChordVolume);
            await Task.Delay(750);
            Dispatcher.UIThread.RunJobs();
            saved = (AppSettings)converter.DeserializeSettings(await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml")), typeof(AppSettings));
            Assert.False(saved.Sync.Playback.UseSeparateChordVolume);
            Assert.Equal(70, saved.Audio.ChordVolume);
        }
        finally { Directory.Delete(folder, true); }
    }

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
