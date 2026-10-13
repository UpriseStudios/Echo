using System;
using System.IO;
using System.Threading.Tasks;
using Echo.Core.Challenges;
using Echo.Core.Reels;
using Echo.Game.Clips;
using Echo.Game.UI;
using Echo.Services;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// The clip editor (a user revision): pick any stretch of a reel on a timeline, preview it, then make a GIF
    /// (up to 10 s) or an MP4 video with the game's music and sound (up to 60 s, for TikTok and YouTube). While the
    /// editor is open the world camera draws into the 9:16 clip texture the editor shows, so the preview is exactly
    /// the clip. The chosen stretch plays in real time while it records; the file is shared with the run's
    /// challenge link.
    /// </summary>
    public sealed partial class EchoGame
    {
        const float SeekEvery = 0.15f;   // while a handle is dragged, the preview catches up this often

        [SerializeField] ClipEditorView _clipEditor;
        [SerializeField] ClipAudioTap _audioTap;

        ClipStage _clipStage;
        ClipFormat _clipFormat;
        int _clipStart, _clipEnd;
        ClipCapture _capture;
        Camera _worldCamera;
        GifClipWriter _gif;
        IVideoClipWriter _video;
        int _clipNextTick, _clipFrames;
        long _clipAudioFrames;
        float[] _audioScratch;
        bool _seekPending;
        int _seekTarget;
        float _seekAt;

        /// <summary>The last clip written, for tests.</summary>
        public string LastClipPath { get; private set; }

        public ClipStage ClipStage => _clipStage;
        public bool MakingClip => _clipStage == ClipStage.Recording || _clipStage == ClipStage.Saving;
        public ClipRange ClipRange => new ClipRange(_clipStart, _clipEnd);

        int ClipLength => ClipRange.Length(_reelPlayer.Reel.Run.DeathTick);

        // How far into the reel the clip is: the run's tick until it ends, then the time since its death.
        int ClipTick => _replayEnded
            ? _reelPlayer.Reel.Run.DeathTick + Mathf.FloorToInt(_replayEndedFor * ClipRange.TicksPerSecond)
            : _run.Tick;

        void WireClipEditor()
        {
            if (_clipEditor == null) return;
            _clipEditor.HandleMoved += OnClipHandleMoved;
            _clipEditor.HandleReleased += () => { if (_seekPending) SeekNow(); };
            _clipEditor.GifClicked += () => SetClipFormat(ClipFormat.Gif);
            _clipEditor.VideoClicked += () => SetClipFormat(ClipFormat.Video);
            _clipEditor.PlayClicked += ToggleClipPreview;
            _clipEditor.MakeClicked += MakeClip;
            _clipEditor.BackClicked += EndReplay;
            _clipEditor.CancelClicked += CancelRecording;
        }

        void OpenClipEditor(int index)
        {
            if (_clipEditor == null || !SetUpReplay(index)) return;
            _replay.Hide();
            _hud.SetVisible(false);
            _capture ??= new ClipCapture();
            _worldCamera = _cameraRig.GetComponent<Camera>();
            _worldCamera.targetTexture = _capture.Target;
            _worldCamera.enabled = false;   // drawn by hand after each frame's views, so it can be read back in step
            _clipStage = ClipStage.Editing;
            _clipEditor.Show(_capture.Target, VideoClipWriters.Supported);
            SetClipFormat(ClipFormat.Gif);
            ClipRange range = ClipRange.Default(_clipFormat, _reelPlayer.Reel.Run.DeathTick);
            SetClipRange(range.Start, range.End);
            SeekReplay(_clipStart);
            SetState(GameState.Replay);
        }

        void CloseClipEditor()
        {
            if (_clipStage == ClipStage.None) return;
            if (_clipStage == ClipStage.Recording) CancelRecording();
            _clipStage = ClipStage.None;
            _clipEditor.Hide();
            if (_worldCamera != null)
            {
                _worldCamera.targetTexture = null;
                _worldCamera.enabled = true;
            }
            ApplySettings();
        }

        /// <summary>Sets the clip's stretch, within the format's limits. Also a test hook.</summary>
        public void SetClipRange(int start, int end, bool movingEnd = true)
        {
            ClipRange r = ClipRange.Clamp(start, end, _clipFormat, _reelPlayer.Reel.Run.DeathTick, movingEnd);
            _clipStart = r.Start;
            _clipEnd = r.End;
            string kind = _clipFormat == ClipFormat.Gif ? $"GIF · up to {ClipRange.GifMaxTicks / ClipRange.TicksPerSecond} s" : $"Video · up to {ClipRange.VideoMaxTicks / ClipRange.TicksPerSecond} s";
            _clipEditor.SetRange(r.Start, r.End, ClipLength, ClipRange.Time(r.Start), ClipRange.Time(r.End), $"{r.Seconds:0.0} s  ·  {kind}");
        }

        /// <summary>GIF or video; the stretch is trimmed to the format's limit from its end. Also a test hook.</summary>
        public void SetClipFormat(ClipFormat format)
        {
            if (format == ClipFormat.Video && !VideoClipWriters.Supported) return;
            _clipFormat = format;
            _clipEditor.SetFormat(format == ClipFormat.Video);
            if (_reelPlayer != null && _clipEnd > 0) SetClipRange(_clipStart, _clipEnd);
        }

        void OnClipHandleMoved(bool end, float at)
        {
            if (_clipStage != ClipStage.Editing && _clipStage != ClipStage.Previewing) return;
            if (_clipStage == ClipStage.Previewing) StopClipPreview(seek: false);
            int tick = Mathf.RoundToInt(at * ClipLength);
            if (end) SetClipRange(_clipStart, tick, movingEnd: true);
            else SetClipRange(tick, _clipEnd, movingEnd: false);
            _seekTarget = end ? _clipEnd : _clipStart;
            _seekPending = true;
        }

        void TickClipEditing()
        {
            if (_seekPending && Time.unscaledTime >= _seekAt) SeekNow();
        }

        void SeekNow()
        {
            _seekPending = false;
            _seekAt = Time.unscaledTime + SeekEvery;
            SeekReplay(_seekTarget);
        }

        // Shows the reel at a tick: played forward silently, from the start again when going back.
        void SeekReplay(int tick)
        {
            Reel reel = _reelPlayer.Reel;
            if (tick < _run.Tick || _replayEnded) RestartReelPlayer(reel);
            _sfx.SetEnabled(false);
            while (_run.Tick < tick && !_reelPlayer.Finished)
            {
                if (_reelPlayer.ContinueDue) _reelPlayer.Continue();
                _reelPlayer.Step();
            }
            if (_reelPlayer.Finished)
            {
                _replayEnded = true;
                _replayEndedFor = Mathf.Max(0, tick - reel.Run.DeathTick) / (float)ClipRange.TicksPerSecond;
            }
            SnapshotPrevious();
            _livesEarnedSeen = _run.LivesEarned;
            _livesUsedSeen = _run.LivesUsed;
            _shieldBreaksSeen = _run.ShieldBreaks;
            _ghostViews.ResetPop();
            _driver.Reset();
            ApplySettings();
        }

        void ToggleClipPreview()
        {
            if (_clipStage == ClipStage.Previewing)
            {
                StopClipPreview(seek: true);
                return;
            }
            if (_clipStage != ClipStage.Editing) return;
            SeekReplay(_clipStart);
            _clipStage = ClipStage.Previewing;
            _clipEditor.SetPlaying(true);
        }

        void StopClipPreview(bool seek)
        {
            _clipStage = ClipStage.Editing;
            _clipEditor.SetPlaying(false);
            if (seek) SeekReplay(_clipStart);
        }

        void MakeClip()
        {
            if (_clipStage != ClipStage.Editing && _clipStage != ClipStage.Previewing) return;
            if (_clipStage == ClipStage.Previewing) StopClipPreview(seek: false);
            SeekReplay(_clipStart);
            string path = Path.Combine(Application.temporaryCachePath, _clipFormat == ClipFormat.Gif ? "echo_clip.gif" : "echo_clip.mp4");
            if (_clipFormat == ClipFormat.Video)
            {
                _video = VideoClipWriters.Create();
                if (_video == null || !_video.Begin(path, ClipCapture.Width, ClipCapture.Height, VideoClipWriters.Scale,
                        VideoClipWriters.Fps, ClipAudioTap.SampleRate, ClipAudioTap.Channels))
                {
                    _video = null;
                    _clipEditor.SetStatus("Video isn't available on this device. Try a GIF.");
                    return;
                }
                if (_audioTap != null) _audioTap.Begin();
            }
            else _gif = new GifClipWriter(ClipCapture.Width, ClipCapture.Height);
            LastClipPath = null;
            _clipPath = path;
            _clipNextTick = _clipStart;
            _clipFrames = 0;
            _clipAudioFrames = 0;
            _clipStage = ClipStage.Recording;
            _clipEditor.SetBusy(true, recording: true);
        }

        string _clipPath;

        // After the frame's views are placed: draw the world into the clip texture, then record when it is due.
        void CaptureClipFrame()
        {
            if (_clipStage == ClipStage.None || _worldCamera == null) return;
            _worldCamera.Render();
            int tick = ClipTick;
            _clipEditor.SetPlayhead(Mathf.Min(tick, ClipLength), ClipLength, _clipStage != ClipStage.Editing);
            if (_clipStage == ClipStage.Previewing && tick >= _clipEnd) StopClipPreview(seek: true);
            if (_clipStage != ClipStage.Recording) return;

            int step = _clipFormat == ClipFormat.Gif ? GifClipWriter.TicksPerFrame : VideoClipWriters.TicksPerFrame;
            if (tick >= _clipNextTick)
            {
                _capture.Read(_run.Score, _run.GhostsAtStart, _reelPlayer.Reel.NewBest);
                // A slow frame owes more than one: the same picture repeats so the clip keeps its timing.
                while (tick >= _clipNextTick && _clipNextTick < _clipEnd)
                {
                    if (_gif != null) _gif.AddFrame(_capture.Frame);
                    else _video.AddFrame(_capture.Frame);
                    _clipFrames++;
                    _clipNextTick += step;
                }
            }
            if (_video != null) PassAudio();
            _clipEditor.SetStatus($"Recording  {ClipRange.Time(Mathf.Min(tick, _clipEnd) - _clipStart)} / {ClipRange.Time(_clipEnd - _clipStart)}");
            if (tick >= _clipEnd) FinishRecording();
        }

        void PassAudio()
        {
            if (_audioTap == null) return;
            int frames = _audioTap.Drain(ref _audioScratch);
            if (frames <= 0) return;
            _video.AddAudio(_audioScratch, frames);
            _clipAudioFrames += frames;
        }

        // The sound track is made as long as the picture, with silence where nothing was heard (or no audio runs).
        void PadAudio()
        {
            if (ClipAudioTap.SampleRate <= 0) return;
            long wanted = (long)_clipFrames * ClipAudioTap.SampleRate / VideoClipWriters.Fps;
            int chunk = 4096;
            if (_audioScratch == null || _audioScratch.Length < chunk * ClipAudioTap.Channels) _audioScratch = new float[chunk * ClipAudioTap.Channels];
            Array.Clear(_audioScratch, 0, chunk * ClipAudioTap.Channels);
            while (_clipAudioFrames < wanted)
            {
                int n = (int)Math.Min(chunk, wanted - _clipAudioFrames);
                _video.AddAudio(_audioScratch, n);
                _clipAudioFrames += n;
            }
        }

        async void FinishRecording()
        {
            _clipStage = ClipStage.Saving;
            _clipEditor.SetBusy(true, recording: false);
            _clipEditor.SetStatus(_gif != null ? "Making your GIF..." : "Finishing your video...");
            Reel reel = _reelPlayer.Reel;
            string path = _clipPath, mime = _gif != null ? "image/gif" : "video/mp4";
            bool made;
            try
            {
                if (_gif != null)
                {
                    GifClipWriter gif = _gif;
                    _gif = null;
                    await gif.SaveAsync(path);
                    made = true;
                }
                else
                {
                    if (_audioTap != null) _audioTap.End();
                    PassAudio();
                    PadAudio();
                    IVideoClipWriter video = _video;
                    _video = null;
                    made = await video.FinishAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Echo] clip failed: " + e.Message);
                made = false;
            }
            if (_clipStage != ClipStage.Saving || _reelPlayer == null || _reelPlayer.Reel != reel) return; // left meanwhile
            _clipStage = ClipStage.Editing;
            _clipEditor.SetBusy(false, recording: false);
            SeekReplay(_clipStart);
            if (!made)
            {
                _clipEditor.SetStatus("The clip couldn't be made.");
                return;
            }
            LastClipPath = path;
            _clipEditor.SetStatus(mime == "video/mp4" ? "Video ready to share" : "GIF ready to share");
            string link = await ChallengeLinkFor(reel, _save.Settings.PlayerName ?? "");
            _services.Share.ShareFile(path, mime, ClipText(reel, link));
        }

        void CancelRecording()
        {
            if (_clipStage != ClipStage.Recording) return;
            if (_audioTap != null) _audioTap.End();
            _gif = null;
            if (_video != null)
            {
                IVideoClipWriter video = _video;
                _video = null;
                _ = video.FinishAsync();   // closes the encoder; the file is not used
            }
            _clipStage = ClipStage.Editing;
            _clipEditor.SetBusy(false, recording: false);
            _clipEditor.SetStatus("Cancelled");
            SeekReplay(_clipStart);
        }

        // The clip travels with the run's challenge link, so whoever sees it can race it.
        string ClipText(Reel reel, string link) =>
            $"{reel.Score:N0} in Echo with {Plural(reel.GhostsAtStart, "ghost")} after me. Race my run:\n{link}{StoreLine()}";
    }
}
