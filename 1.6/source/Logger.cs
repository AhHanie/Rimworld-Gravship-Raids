using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Gravship_Raids
{
    public static class Logger
    {
        private const string Prefix = "[Gravship Raids] ";

        private static bool Enabled => GravshipRaidsSettings.debugLogging;

        public static void Message(string message)
        {
            if (Enabled)
            {
                Log.Message(Prefix + message);
            }
        }

        public static void Warning(string message)
        {
            if (Enabled)
            {
                Log.Warning(Prefix + message);
            }
        }

        public static void Error(string message)
        {
            if (Enabled)
            {
                Log.Error(Prefix + message);
            }
        }

        public static void Exception(Exception exception, string context = null)
        {
            if (exception == null || !Enabled)
            {
                return;
            }

            string prefix = string.IsNullOrWhiteSpace(context) ? Prefix : Prefix + context + ": ";
            Log.Error(prefix + exception);
        }
    }
}
