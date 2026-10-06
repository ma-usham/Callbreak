using System;
using Callbreak.Application.Ports;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The Profile screen's words for ADR 041's week between name changes: the note shown before
    /// Save while a valid new name is typed, and the line under a field that takes no typing yet,
    /// which counts whole days (rounded up) from two days on, hours below that, and says "within
    /// the hour" for the last one.
    /// </summary>
    public static class NameWait
    {
        public const string WeekNoteKey = "ui.profile.name_week_note";
        public const string DaysKey = "ui.profile.name_wait_days";
        public const string HoursKey = "ui.profile.name_wait_hours";
        public const string LastHourKey = "ui.profile.name_wait_soon";

        /// <summary>"You can change your name again in 6 days." for <paramref name="wait"/> (more than zero).</summary>
        public static string Text(TimeSpan wait, ILocalization text)
        {
            int days = (int)Math.Ceiling(wait.TotalDays);
            if (days >= 2)
            {
                return text.Format(DaysKey, days);
            }

            int hours = (int)Math.Ceiling(wait.TotalHours);
            return hours >= 2 ? text.Format(HoursKey, hours) : text.Get(LastHourKey);
        }
    }
}
