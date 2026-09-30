using System;
using System.Globalization;
using System.Text;
using Callbreak.Application.Ports;
using UnityEngine;

namespace Callbreak.Infrastructure.Services
{
    /// <summary>
    /// <see cref="IAnalytics"/> that writes each event and its parameters to the console, in the
    /// Editor and development builds only; release builds send and log nothing. It stands in for UGS
    /// Analytics until the project is linked to Unity Cloud. Callers pass player ids only, never
    /// names or hands (GDD §15).
    /// </summary>
    public sealed class LogAnalytics : IAnalytics
    {
        private readonly bool _enabled;

        public LogAnalytics()
        {
            _enabled = Debug.isDebugBuild;
        }

        public void Track(string eventName, params (string key, object value)[] parameters)
        {
            if (!_enabled || string.IsNullOrEmpty(eventName))
            {
                return;
            }

            var sb = new StringBuilder("[Analytics] ").Append(eventName);
            if (parameters != null && parameters.Length > 0)
            {
                sb.Append(" {");
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(parameters[i].key).Append('=').Append(Format(parameters[i].value));
                }

                sb.Append('}');
            }

            Debug.Log(sb.ToString());
        }

        private static string Format(object value)
        {
            if (value == null)
            {
                return "null";
            }

            return value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
        }
    }
}
