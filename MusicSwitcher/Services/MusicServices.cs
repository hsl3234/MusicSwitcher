using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.Media.Control;
using MusicSwitcher.Model;
using System.Security.Cryptography;
using Application = System.Windows.Application;

namespace MusicSwitcher.Services
{
    public interface IMusicServices : IDisposable
    {
        /// <summary>Включение следующей песни</summary>
        Task NextButton();

        /// <summary>Включение предыдущей песни</summary>
        Task BackButton();

        /// <summary>Старт или остановка песни</summary>
        Task StartStop();

        /// <summary>Обновление модели</summary>
        Task UpdateMusic();

        /// <summary>Инициализация подписок на события</summary>
        Task InitializeAsync();
    }

    /// <summary>
    /// Сервис по переключению музыки с поддержкой событий
    /// </summary>
    public class MusicServices : IMusicServices
    {
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly MusicModel _musicModel;
        private readonly Dispatcher _dispatcher;
        private bool _disposed;

        public MusicServices(MusicModel musicModel)
        {
            _musicModel = musicModel;
            _dispatcher = Application.Current.Dispatcher;
        }

        /// <summary>
        /// Инициализация менеджера и подписка на события
        /// </summary>
        public async Task InitializeAsync()
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            // Подписка на смену текущей сессии (переключение плеера)
            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;

            // Подписка на текущую сессию
            SubscribeToCurrentSession();

            // Первоначальное обновление
            await UpdateMusic();
        }

        private void SubscribeToCurrentSession()
        {
            // Отписываемся от старой сессии
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }

            _currentSession = _sessionManager?.GetCurrentSession();

            if (_currentSession != null)
            {
                // Подписываемся на события новой сессии
                _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
          //  Console.WriteLine("[Event] CurrentSessionChanged — плеер сменился");
            SubscribeToCurrentSession();
            InvokeOnUIThread(() => _ = UpdateMusic());
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
           // Console.WriteLine("[Event] MediaPropertiesChanged — трек изменился");
            InvokeOnUIThread(() => _ = UpdateMusic());
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
          //  Console.WriteLine("[Event] PlaybackInfoChanged — статус воспроизведения изменился");
            InvokeOnUIThread(() => _ = UpdateMusic());
        }

        /// <summary>
        /// Выполняет действие в UI потоке
        /// </summary>
        private void InvokeOnUIThread(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.BeginInvoke(action);
            }
        }

        public async Task NextButton()
        {
            try
            {
                var session = _sessionManager?.GetCurrentSession();
                if (session != null)
                {
                    await session.TrySkipNextAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"NextButton error: {e.Message}");
            }
        }

        public async Task BackButton()
        {
            try
            {
                var session = _sessionManager?.GetCurrentSession();
                if (session != null)
                {
                    await session.TrySkipPreviousAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"BackButton error: {e.Message}");
            }
        }

        public async Task StartStop()
        {
            try
            {
                var session = _sessionManager?.GetCurrentSession();
                if (session == null) return;

                var playback = session.GetPlaybackInfo();
                if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                {
                    await session.TryPlayAsync();
                }
                else
                {
                    await session.TryPauseAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"StartStop error: {e.Message}");
            }
        }

        public async Task UpdateMusic()
        {
            try
            {
                var session = _sessionManager?.GetCurrentSession();
                if (session == null)
                {
                    await _musicModel.SetDefault();
                    return;
                }

                var mediaProperties = await session.TryGetMediaPropertiesAsync();
                var playback = session.GetPlaybackInfo();

                // Обновляем информацию о треке если изменилась
                if (_musicModel.AlbumName != mediaProperties.AlbumTitle ||
                    _musicModel.SingName != mediaProperties.Title ||
                    _musicModel.Status != playback.PlaybackStatus.ToString())
                {
                    await _musicModel.UpdateMusic(
                        mediaProperties.Title,
                        mediaProperties.AlbumTitle,
                        mediaProperties.Artist,
                        playback.PlaybackStatus.ToString());
                }

                // Обновляем обложку
                if (mediaProperties.Thumbnail != null)
                {
                    var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                    using var md5 = MD5.Create();
                    using var memstream = new MemoryStream();

                    var buffer = new byte[4096];
                    var bytesRead = 0;
                    var inputStream = stream.AsStreamForRead();

                    while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        memstream.Write(buffer, 0, bytesRead);
                    }

                    var bytes = memstream.ToArray();
                    var hash = Convert.ToBase64String(md5.ComputeHash(bytes));

                    if (_musicModel.HashImage != hash)
                    {
                        await _musicModel.UpdatePicture(bytes);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"UpdateMusic error: {e.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }

            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
