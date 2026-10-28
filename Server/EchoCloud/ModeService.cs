using System.Collections.Generic;
using Echo.Core.Modes;

namespace EchoCloud
{
    /// <summary>
    /// The modes on show (modes doc, the Modes screen) and the gate every mode endpoint checks, so a mode switched off
    /// or out of its dates is closed for every game version. Development can force every mode open.
    /// </summary>
    public static class ModeService
    {
        public static GetModesResponse Describe(IReadOnlyList<ModeWindow> windows, long now, bool forceAll)
        {
            var response = new GetModesResponse { Ok = true, Now = now };
            foreach (string id in ModeIds.All)
            {
                if (forceAll)
                {
                    response.Modes.Add(new ModeStatus { Id = id, Open = true, SecondsLeft = (now / ModeRules.Day + 1) * ModeRules.Day - now });
                    continue;
                }
                ModeWindow w = ModeRules.Pick(windows, id, now);
                if (w == null || !ModeRules.IsShown(w, now)) continue;
                response.Modes.Add(new ModeStatus { Id = id, Open = ModeRules.IsOpen(w, now), SecondsLeft = ModeRules.SecondsLeft(w, now) });
            }
            return response;
        }

        /// <summary>Whether a mode's endpoints are open now (forced: development's override).</summary>
        public static bool IsOpen(IReadOnlyList<ModeWindow> windows, string id, long now, bool forced)
        {
            if (forced) return true;
            ModeWindow w = ModeRules.Pick(windows, id, now);
            return w != null && ModeRules.IsOpen(w, now);
        }
    }
}
