using System.Text;
using TMPro;
using UnityEngine;

namespace Echo.Game.UI
{
    /// <summary>
    /// Google Play Games on PC (a user revision: Echo on PC through Play Games): there, every "tap" a player reads says
    /// "click" (Play's PC guidelines), on every text, including cards made at runtime, without changing the game's
    /// strings. Installed once at startup on PC only; a text without "tap" in it is passed through untouched with no
    /// allocation, so the HUD's per-frame text stays allocation-free.
    /// </summary>
    public sealed class PcWording : ITextPreprocessor
    {
        static readonly PcWording Instance = new PcWording();
        static bool? _isPc;

        /// <summary>Tests: behave as on PC (or not), whatever the device.</summary>
        public static bool? ForcePc;

        /// <summary>Running in Google Play Games on PC (the emulator reports HPE_EXPERIENCE).</summary>
        public static bool IsPc
        {
            get
            {
                if (ForcePc.HasValue) return ForcePc.Value;
                if (_isPc.HasValue) return _isPc.Value;
                _isPc = false;
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var packages = activity.Call<AndroidJavaObject>("getPackageManager");
                    _isPc = packages.Call<bool>("hasSystemFeature", "com.google.android.play.feature.HPE_EXPERIENCE");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Echo] PC check failed: " + e.Message);
                }
#endif
                return _isPc.Value;
            }
        }

        /// <summary>On PC, gives every text the click wording from now on.</summary>
        public static void InstallIfPc()
        {
            if (!IsPc) return;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Adopt(text);
        }

        // Texts made later (cloned cards) get it the first time they draw.
        static void OnTextChanged(Object changed)
        {
            if (changed is TMP_Text text && IsPc) Adopt(text);
        }

        static void Adopt(TMP_Text text)
        {
            if (text.textPreprocessor == Instance) return;
            text.textPreprocessor = Instance;
            text.SetAllDirty();
        }

        public string PreprocessText(string text) => IsPc ? Clicks(text) : text;

        /// <summary>"Tap to run" as "Click to run": tap, taps, tapped and tapping, in any case, as whole words.</summary>
        public static string Clicks(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("ap", System.StringComparison.OrdinalIgnoreCase) < 0) return text;
            StringBuilder sb = null;
            int copied = 0;
            for (int i = 0; i + 3 <= text.Length; i++)
            {
                if (char.ToLowerInvariant(text[i]) != 't' || char.ToLowerInvariant(text[i + 1]) != 'a' || char.ToLowerInvariant(text[i + 2]) != 'p') continue;
                if (i > 0 && char.IsLetter(text[i - 1])) continue;
                int end = i + 3;
                string tail = "";
                foreach (string ending in new[] { "ping", "ped", "s" })
                    if (string.Compare(text, end, ending, 0, ending.Length, System.StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        tail = text.Substring(end, ending.Length);
                        end += ending.Length;
                        break;
                    }
                if (end < text.Length && char.IsLetter(text[end])) continue;
                bool upper = char.IsUpper(text[i + 1]);
                string click = upper ? "CLICK" : char.IsUpper(text[i]) ? "Click" : "click";
                string ending2 = tail.Length == 0 ? "" : tail.ToLowerInvariant() switch { "ping" => "ing", "ped" => "ed", _ => "s" };
                if (upper) ending2 = ending2.ToUpperInvariant();
                sb ??= new StringBuilder(text.Length + 8);
                sb.Append(text, copied, i - copied).Append(click).Append(ending2);
                copied = end;
                i = end - 1;
            }
            return sb == null ? text : sb.Append(text, copied, text.Length - copied).ToString();
        }
    }
}
