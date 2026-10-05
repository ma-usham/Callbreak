using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// <see cref="IDialogs"/> over the one <see cref="DialogView"/> (GDD §23 popup layer). One
    /// dialog at a time: asking again dismisses the open one first. An open dialog that is not
    /// <see cref="DialogRequest.Dismissable"/> ("Update required") stays: a tap outside its panel and
    /// Android back do nothing, and a newer dialog is answered Dismissed at once, unseen.
    /// </summary>
    public sealed class DialogService : IDialogs, IDisposable
    {
        private readonly DialogView _view;
        private readonly ISettingsService _settings;
        private readonly IAudioService _audio;

        private TaskCompletionSource<DialogChoice> _pending;
        private CancellationTokenRegistration _cancellation;

        // The open dialog is not dismissable.
        private bool _holding;

        public DialogService(DialogView view, ISettingsService settings, IAudioService audio)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _settings = settings;
            _audio = audio;
            _view.Confirmed += OnConfirmed;
            _view.Cancelled += OnCancelled;
            _view.Dismissed += OnDismissed;
        }

        public bool IsOpen => _pending != null;

        public async Task<bool> ConfirmAsync(DialogRequest request, CancellationToken ct)
        {
            return await AskAsync(request, ct) == DialogChoice.Confirm;
        }

        public Task<DialogChoice> AskAsync(DialogRequest request, CancellationToken ct)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (_holding && IsOpen)
            {
                return Task.FromResult(DialogChoice.Dismissed);
            }

            Complete(DialogChoice.Dismissed);
            var pending = new TaskCompletionSource<DialogChoice>();
            _pending = pending;
            _holding = !request.Dismissable;
            if (ct.CanBeCanceled)
            {
                _cancellation = ct.Register(() =>
                {
                    if (_pending == pending)
                    {
                        Complete(DialogChoice.Dismissed);
                    }
                });
            }

            // A token cancelled before the dialog could open: nothing to show.
            if (_pending != pending)
            {
                return pending.Task;
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

            if (!_holding)
            {
                _audio.Play(SoundCue.UiBack);
                Complete(DialogChoice.Dismissed);
            }

            return true;
        }

        public void Dispose()
        {
            _view.Confirmed -= OnConfirmed;
            _view.Cancelled -= OnCancelled;
            _view.Dismissed -= OnDismissed;
            Complete(DialogChoice.Dismissed);
        }

        private void OnConfirmed()
        {
            _audio.Play(SoundCue.UiTap);
            Complete(DialogChoice.Confirm);
        }

        private void OnCancelled()
        {
            _audio.Play(SoundCue.UiBack);
            Complete(DialogChoice.Cancel);
        }

        private void OnDismissed()
        {
            if (_holding)
            {
                return;
            }

            _audio.Play(SoundCue.UiBack);
            Complete(DialogChoice.Dismissed);
        }

        private void Complete(DialogChoice choice)
        {
            TaskCompletionSource<DialogChoice> pending = _pending;
            if (pending == null)
            {
                return;
            }

            _pending = null;
            _holding = false;
            _cancellation.Dispose();
            _cancellation = default;
            if (_view != null)
            {
                _view.Hide();
            }

            pending.TrySetResult(choice);
        }
    }
}
