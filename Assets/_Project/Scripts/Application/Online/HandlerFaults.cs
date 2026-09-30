using System;
using System.Runtime.ExceptionServices;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// Raises events to handlers the online classes do not own (screens, stats) so that one that
    /// throws neither stops the handlers after it nor leaves the room half-changed: every handler
    /// runs, the first exception is kept, and <see cref="Rethrow"/> hands it on once the owner has
    /// finished its own work for the message, to be logged where the message came in (the relay
    /// transport or the app's ticker).
    /// </summary>
    internal sealed class HandlerFaults
    {
        private Exception _first;

        /// <summary>True once a handler has thrown since the last <see cref="Rethrow"/>.</summary>
        public bool Any => _first != null;

        public void Raise(Action handlers)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Keep(e);
                }
            }
        }

        public void Raise<T>(Action<T> handlers, T arg)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler)(arg);
                }
                catch (Exception e)
                {
                    Keep(e);
                }
            }
        }

        public void Raise<T1, T2>(Action<T1, T2> handlers, T1 first, T2 second)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T1, T2>)handler)(first, second);
                }
                catch (Exception e)
                {
                    Keep(e);
                }
            }
        }

        /// <summary>Remembers <paramref name="e"/> unless an earlier exception is already kept.</summary>
        public void Keep(Exception e)
        {
            if (_first == null)
            {
                _first = e;
            }
        }

        /// <summary>Throws the kept exception, with its own stack trace, and forgets it; does nothing when none was kept.</summary>
        public void Rethrow()
        {
            Exception first = _first;
            if (first == null)
            {
                return;
            }

            _first = null;
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }
}
