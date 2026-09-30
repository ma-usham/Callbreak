using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>
    /// Runs code under a given current culture and restores the original. Arabic (Saudi Arabia) and
    /// Swedish are useful probes: on ICU they format negative numbers with a different sign.
    /// </summary>
    public static class CultureRunner
    {
        public static string In(string cultureName, Func<string> action)
        {
            CultureInfo culture;
            try
            {
                culture = new CultureInfo(cultureName);
            }
            catch (CultureNotFoundException)
            {
                Assert.Ignore("Culture " + cultureName + " is not available on this runtime.");
                return null;
            }

            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = culture;
                return action();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
