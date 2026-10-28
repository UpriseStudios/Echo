using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// Recording mode (marketing content plan): for screen recordings of real play. It hides the DEV SERVER tag and every
    /// button that leads to an ad, and can hide the HUD during runs. It changes nothing about how a run plays (no
    /// invincibility, nothing a real player couldn't do). Offered in development builds and, after five taps on the
    /// Settings heading, in release builds too, so footage can show the live board. It lasts for the session.
    /// </summary>
    public sealed partial class EchoGame
    {
        bool _recordingMode, _recordingHidesHud;
        bool _recordingOffered;

        public bool RecordingMode => _recordingMode;
        bool HudHiddenForRecording => _recordingMode && _recordingHidesHud;

        void WireRecording()
        {
            _settings.RecordingClicked += () =>
            {
                _recordingMode = !_recordingMode;
                ApplyRecordingMode();
            };
            _settings.RecordingHudClicked += () =>
            {
                _recordingHidesHud = !_recordingHidesHud;
                ApplyRecordingMode();
            };
        }

        /// <summary>Test hook and the Settings buttons.</summary>
        public void SetRecordingMode(bool on, bool hideHud)
        {
            _recordingMode = on;
            _recordingHidesHud = hideHud;
            ApplyRecordingMode();
        }

        void ApplyRecordingMode()
        {
            _settings.SetRecording(_recordingOffered || Debug.isDebugBuild, _recordingMode, _recordingHidesHud);
            _title.SetAdButtons(!_recordingMode);
            _title.SetDevServer(!_recordingMode && _serviceConfig != null && _serviceConfig.ShowsDevServerTag);
            _deathScreen.SetAdsHidden(_recordingMode);
            if (State == GameState.Running) _hud.SetVisible(!HudHiddenForRecording);
        }
    }
}
