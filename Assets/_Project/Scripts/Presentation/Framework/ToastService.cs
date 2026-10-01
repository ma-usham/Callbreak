using System;
using System.Collections.Generic;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// <see cref="IToast"/> over a <see cref="ToastView"/> (GDD §21 scene scopes, §23 toast layer).
    /// Toasts show one at a time, in order. A text that is already on screen or already waiting is
    /// not queued twice, and at most <see cref="MaxWaiting"/> wait: the oldest waiting one is
    /// dropped first. Texts arrive already localised. With Reduce motion they only fade.
    /// </summary>
    public sealed class ToastService : IToast, IDisposable
    {
        public const int MaxWaiting = 3;

        private readonly ToastView _view;
        private readonly ISettingsService _settings;
        private readonly Queue<string> _waiting = new Queue<string>();

        public ToastService(ToastView view, ISettingsService settings)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            _view = view;
            _settings = settings;
            _view.Finished += Pump;
        }

        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _view == null)
            {
                return;
            }

            if ((_view.IsShowing && _view.Text == text) || _waiting.Contains(text))
            {
                return;
            }

            _waiting.Enqueue(text);
            while (_waiting.Count > MaxWaiting)
            {
                _waiting.Dequeue();
            }

            Pump();
        }

        public void Dispose()
        {
            _waiting.Clear();
            if (_view != null)
            {
                _view.Finished -= Pump;
            }
        }

        private void Pump()
        {
            if (_view == null || _view.IsShowing || !_view.isActiveAndEnabled || _waiting.Count == 0)
            {
                return;
            }

            _view.ReduceMotion = _settings != null && _settings.ReduceMotion;
            _view.Show(_waiting.Dequeue());
        }
    }
}
