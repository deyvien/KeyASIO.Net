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
    public async Task JudgementThresholdChildDefaultsOffPersistsAndRequiresParent()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var converter = MyYamlConfigurationConverter.Instance;
            var settings = ConfigurationFactory.GetConfiguration<AppSettings>(folder, "appsettings.yaml", converter);
            Assert.False(settings.Sync.Playback.UseChordJudgementThresholds);
            using var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, settings,
                Mock.Of<IPlaybackEngine>(), Mock.Of<ISukiToastManager>());
            foreach (bool enabled in new[] { true, false })
            {
                settings.Sync.Playback.UseChordJudgementThresholds = enabled;
                Assert.False(settings.Sync.Playback.ChordJudgementThresholdsActive);
                settings.Sync.Playback.WaitForFinalChordKey = true;
                Assert.Equal(enabled, settings.Sync.Playback.ChordJudgementThresholdsActive);
                settings.Sync.Playback.WaitForFinalChordKey = false;
                Assert.Equal(enabled, settings.Sync.Playback.UseChordJudgementThresholds);
                await Task.Delay(750);
                Dispatcher.UIThread.RunJobs();
                var saved = (AppSettings)converter.DeserializeSettings(await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml")), typeof(AppSettings));
                Assert.Equal(enabled, saved.Sync.Playback.UseChordJudgementThresholds);
                Assert.False(saved.Sync.Playback.ChordJudgementThresholdsActive);
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    [AvaloniaFact]
    public async Task MapperOverrideDefaultsOffAndPersistsBothValues()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var converter = MyYamlConfigurationConverter.Instance;
            var settings = ConfigurationFactory.GetConfiguration<AppSettings>(folder, "appsettings.yaml", converter);
            Assert.False(settings.Sync.Playback.RampOverrideMapperVolume);
            using var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, settings,
                Mock.Of<IPlaybackEngine>(), Mock.Of<ISukiToastManager>());
            foreach (bool enabled in new[] { true, false })
            {
                settings.Sync.Playback.RampOverrideMapperVolume = enabled;
                await Task.Delay(750);
                Dispatcher.UIThread.RunJobs();
                var saved = (AppSettings)converter.DeserializeSettings(await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml")), typeof(AppSettings));
                Assert.Equal(enabled, saved.Sync.Playback.RampOverrideMapperVolume);
                Assert.False(saved.Sync.Playback.AnyVolumeRampActive);
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    [AvaloniaFact]
    public async Task ComboResetDefaultsOffAndPersistsIndependentlyOfRampToggles()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var converter = MyYamlConfigurationConverter.Instance;
            var settings = ConfigurationFactory.GetConfiguration<AppSettings>(folder, "appsettings.yaml", converter);
            Assert.False(settings.Sync.Playback.ResetVolumeRampOnComboBreak);
            using var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, settings,
                Mock.Of<IPlaybackEngine>(), Mock.Of<ISukiToastManager>());
            foreach (bool enabled in new[] { true, false })
            {
                settings.Sync.Playback.ResetVolumeRampOnComboBreak = enabled;
                await Task.Delay(750);
                Dispatcher.UIThread.RunJobs();
                var saved = (AppSettings)converter.DeserializeSettings(await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml")), typeof(AppSettings));
                Assert.Equal(enabled, saved.Sync.Playback.ResetVolumeRampOnComboBreak);
                Assert.False(saved.Sync.Playback.AnyVolumeRampActive);
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    [AvaloniaFact]
    public async Task SingleNoteRampControlsPersistAndDefaultOff()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var converter = MyYamlConfigurationConverter.Instance;
            var settings = ConfigurationFactory.GetConfiguration<AppSettings>(folder, "appsettings.yaml", converter);
            Assert.False(settings.Sync.Playback.RampSingleNoteVolume);
            Assert.False(settings.Sync.Playback.RampChordVolume);
            Assert.False(settings.Sync.Playback.AnyVolumeRampActive);
            Assert.Equal(3, settings.Sync.Playback.SingleNoteRampNotes);
            using var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, settings,
                Mock.Of<IPlaybackEngine>(), Mock.Of<ISukiToastManager>());
            foreach (var curve in Enum.GetValues<KeyAsio.Configuration.Models.SingleNoteRampCurve>())
            {
                settings.Sync.Playback.RampSingleNoteVolume = true;
                settings.Sync.Playback.RampChordVolume = curve == KeyAsio.Configuration.Models.SingleNoteRampCurve.Exponential;
                settings.Sync.Playback.SingleNoteRampCurve = curve;
                settings.Sync.Playback.SingleNoteRampNotes = curve == KeyAsio.Configuration.Models.SingleNoteRampCurve.Linear ? 3 : 10;
                await Task.Delay(750);
                Dispatcher.UIThread.RunJobs();
                var saved = (AppSettings)converter.DeserializeSettings(await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml")), typeof(AppSettings));
                Assert.True(saved.Sync.Playback.RampSingleNoteVolume);
                Assert.Equal(settings.Sync.Playback.RampChordVolume, saved.Sync.Playback.RampChordVolume);
                Assert.Equal(curve, saved.Sync.Playback.SingleNoteRampCurve);
                Assert.Equal(settings.Sync.Playback.SingleNoteRampNotes, saved.Sync.Playback.SingleNoteRampNotes);
            }
            settings.Sync.Playback.RampSingleNoteVolume = false;
            Assert.True(settings.Sync.Playback.AnyVolumeRampActive); // Chords independently keep the shared controls active.
            settings.Sync.Playback.RampChordVolume = false;
            Assert.False(settings.Sync.Playback.AnyVolumeRampActive);
        }
        finally { Directory.Delete(folder, true); }
    }

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
            Assert.Equal(.2f, engine.Object.ChordVolume); // Child alone cannot enable the separate gain.
            var changed = new List<string?>();
            settings.Sync.Playback.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
            settings.Sync.Playback.WaitForFinalChordKey = true;
            Assert.True(settings.Sync.Playback.SeparateChordVolumeActive);
            Assert.Contains(nameof(AppSettingsSyncPlayback.SeparateChordVolumeActive), changed);
            Assert.Equal(.7f, engine.Object.ChordVolume);
            settings.Audio.EffectVolume = 0;
            Assert.Equal(0, engine.Object.EffectVolume);
            Assert.Equal(.7f, engine.Object.ChordVolume);
            await Task.Delay(750);
            Dispatcher.UIThread.RunJobs();
            var saved = (AppSettings)converter.DeserializeSettings(await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml")), typeof(AppSettings));
            Assert.True(saved.Sync.Playback.UseSeparateChordVolume);
            Assert.Equal(70, saved.Audio.ChordVolume);
            settings.Sync.Playback.WaitForFinalChordKey = false;
            Assert.False(settings.Sync.Playback.SeparateChordVolumeActive);
            Assert.True(settings.Sync.Playback.UseSeparateChordVolume); // Saved child choice survives parent off.
            Assert.Equal(0, engine.Object.ChordVolume);
            settings.Sync.Playback.WaitForFinalChordKey = true;
            Assert.Equal(.7f, engine.Object.ChordVolume);
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
