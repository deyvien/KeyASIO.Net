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
            using var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, settings,
                Mock.Of<IPlaybackEngine>(), Mock.Of<ISukiToastManager>());
            foreach (bool enabled in new[] { true, false })
            {
                settings.Sync.Playback.WaitForFinalChordKey = enabled;
                await Task.Delay(750);
                Dispatcher.UIThread.RunJobs();
                var text = await File.ReadAllTextAsync(Path.Combine(folder, "appsettings.yaml"));
                var reloaded = (AppSettings)converter.DeserializeSettings(text, typeof(AppSettings));
                Assert.Equal(enabled, reloaded.Sync.Playback.WaitForFinalChordKey);
            }
        }
        finally { Directory.Delete(folder, true); }
    }
}
