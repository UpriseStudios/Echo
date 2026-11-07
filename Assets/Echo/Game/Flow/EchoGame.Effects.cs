using System.Text;
using Echo.Game.Views;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Echo.Game.Flow
{
    /// <summary>
    /// Time Run art fix, phase 1 (checking the effects are actually on): a plain account of what's drawing the effects,
    /// read from the live objects rather than the settings: the quality mode and why, post-processing and HDR on the
    /// camera that draws the eras, both bloom volumes, the Far Future's lights and the 3D city. Logged at startup (so it
    /// can be read from a phone's log), shown on the benchmark's pass label and its report. Allocates; never per frame.
    /// </summary>
    public sealed partial class EchoGame
    {
        /// <summary>"Full effects (auto: a capable phone)", "Reduced effects (chosen)" and so on.</summary>
        public string EffectsMode
        {
            get
            {
                bool full = FullEffects;
                string why = _save == null ? "no save yet"
                    : _save.Settings.EffectsChosen ? "chosen in Settings"
                    : _benchmarkReduced ? "the benchmark's reduced pass"
                    : WeakDevice ? "auto: a weaker phone"
                    : _sessionReduced ? "auto: the Far Future ran slow this session"
                    : "auto: a capable phone";
                return $"{(full ? "Full" : "Reduced")} effects ({why})";
            }
        }

        public string EffectsReport()
        {
            var s = new StringBuilder();
            s.Append(EffectsMode).Append('\n');
            Camera main = _cameraRig != null ? _cameraRig.GetComponent<Camera>() : null;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            s.Append($"Main camera: post-processing {(_cameraRig != null && _cameraRig.GlowOn ? "on" : "off")}, ")
             .Append($"HDR {(pipeline != null && pipeline.supportsHDR && main != null && main.allowHDR ? "on" : "off")}, ")
             .Append(_cameraRig != null && _cameraRig.PixelPerfect ? "pixel-perfect" : "full resolution").Append('\n');
            foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v.sharedProfile == null || !v.sharedProfile.TryGet(out Bloom bloom)) continue;
                s.Append($"Bloom '{v.name}': weight {v.weight:0.##}, threshold {bloom.threshold.value:0.##}, intensity {bloom.intensity.value:0.##}\n");
            }
            if (_futureLights != null) s.Append($"Far Future lights on: {_futureLights.LightsOn}\n");
            if (_far3D != null) s.Append($"3D city: {_far3D.Showing}{(_far3D.Size.x > 0 ? $", {_far3D.Size.x}x{_far3D.Size.y} HDR" : "")}\n");
            s.Append($"Era: {(_timeRun != null ? _timeRun.Eras[Mathf.Clamp(_eraShown, 0, _timeRun.Eras.Length - 1)].Title : "not in Time Run")}");
            return s.ToString();
        }

        void LogEffects(string when)
        {
            foreach (string line in EffectsReport().Split('\n')) Debug.Log($"[Effects] {when}: {line}");
        }
    }
}
