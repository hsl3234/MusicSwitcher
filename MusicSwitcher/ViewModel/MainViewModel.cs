using MusicSwitcher.Model;
using ReactiveUI;
using System.Reactive;
using MusicSwitcher.Services;
using ReactiveUI.Fody.Helpers;

namespace MusicSwitcher.ViewModel;

public class MainViewModel : ReactiveObject
{
    public MusicModel MusicModel { get; set; }

    private readonly IMusicServices _musicServices;
    private readonly IVolumeService _volumeService;
    private readonly WidgetSettings _settings;

    [Reactive] public double Volume { get; set; } = 1.0;

    public MainViewModel(MusicModel musicModel, IMusicServices musicServices, WidgetSettings settings, IVolumeService volumeService)
    {
        MusicModel = musicModel;
        _musicServices = musicServices;
        _settings = settings;
        _volumeService = volumeService;
        var target = _settings.VolumeTargetProcessName;
        if (!string.IsNullOrEmpty(target))
            Volume = _volumeService.GetVolume(target);
        this.WhenAnyValue(x => x.Volume)
            .Subscribe(v => _volumeService.SetVolume((float)Math.Clamp(v, 0, 1), _settings.VolumeTargetProcessName));
    }

    /// <summary> Синхронизировать значение ползунка с громкостью в микшере (после показа ползунка или смены приложения). </summary>
    public void SyncVolumeFromMixer()
    {
        var target = _settings.VolumeTargetProcessName;
        if (!string.IsNullOrEmpty(target))
            Volume = _volumeService.GetVolume(target);
    }

    public ReactiveCommand<Unit, Task> StartStop => ReactiveCommand.Create(async () =>
    {
        await _musicServices.StartStop();
    });
    public ReactiveCommand<Unit, Task> Next => ReactiveCommand.Create(async () =>
    {
        await  _musicServices.NextButton();
    });
    public ReactiveCommand<Unit, Task> Back => ReactiveCommand.Create(async () =>
    {
        await _musicServices.BackButton();
    });
}