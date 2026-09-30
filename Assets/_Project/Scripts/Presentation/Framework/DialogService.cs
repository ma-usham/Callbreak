using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// <see cref="IDialogs"/> over the one <see cref="DialogView"/> (GDD §23 popup layer). One
    /// dialog at a time: asking again answers the open one with "cancel" first.
    /// </summary>
    public sealed class DialogService : IDialogs, IDisposable
    {
        private readonly DialogView _view;
        private readonly ISettingsService _settings;
        private readonly IAudioService _audio;

        private TaskCompletionSource<bool> _pending;
        private CancellationTokenRegistration _cancellation;

        public DialogService(DialogView view, ISettingsService settings, IAudioService audio)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _settings = settings;
            _audio = audio;
            _view.Confirmed += OnConfirmed;
            _view.Cancelled += OnCancelled;
        }

        public bool IsOpen => _pending != null;

        public Task<bool> ConfirmAsync(DialogRequest request, CancellationToken ct)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Complete(false);
            var pending = new TaskCompletionSource<bool>();
            _pending = pending;
            if (ct.CanBeCanceled)
            {
                _cancellation = ct.Register(() =>
                {
                    if (_pending == pending)
                    {
                        Complete(false);
                    }
                });
            }

            _view.ReduceMotion = _settings.ReduceMotion;
            _view.Show(request.Title, request.Body, request.ConfirmLabel, request.CancelLabel);
            return pending.Task;
        }

        public bool HandleBack()
        {
            if (!IsOpen)
            {
                return false;
            }

            _audio.Play(SoundCue.UiBack);
            Complete(false);
            return true;
        }

        public void Dispose()
        {
            _view.Confirmed -= OnConfirmed;
            _view.Cancelled -= OnCancelled;
            Complete(false);
        }

        private void OnConfirmed()
        {
            _audio.Play(SoundCue.UiTap);
            Complete(true);
        }

        private void OnCancelled()
        {
            _audio.Play(SoundCue.UiBack);
            Complete(false);
        }

        private void Complete(bool result)
        {
            TaskCompletionSource<bool> pending = _pending;
            if (pending == null)
            {
                return;
            }

            _pending = null;
            _cancellation.Dispose();
            _cancellation = default;
            if (_view != null)
            {
                _view.Hide();
            }

            pending.TrySetResult(result);
        }
    }
}
