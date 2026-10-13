using System.Linq;
using Echo.Core.Challenges;
using UnityEditor;

namespace Echo.Editor
{
    /// <summary>
    /// Ghost Challenges arrive as echoghost://challenge/CODE links (a user choice: a custom scheme, no website).
    /// iOS needs the scheme in Player Settings; Android gets its intent filter from AndroidDeepLinks at export.
    /// Run by every scene build, so the setting can't drift.
    /// </summary>
    public static class DeepLinkSetup
    {
        public static void Apply()
        {
            string[] schemes = PlayerSettings.iOS.iOSUrlSchemes ?? new string[0];
            if (!schemes.Contains(ChallengeCode.Scheme))
                PlayerSettings.iOS.iOSUrlSchemes = schemes.Append(ChallengeCode.Scheme).ToArray();
        }
    }
}
