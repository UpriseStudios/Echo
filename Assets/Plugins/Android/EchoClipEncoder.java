package com.uprise.echo;

import android.media.Image;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.os.Build;
import android.util.Log;

import java.io.File;
import java.nio.ByteBuffer;
import java.util.ArrayList;

/**
 * Echo's video clips (a user revision): H.264 + AAC into an MP4 with the phone's own encoders (MediaCodec) and
 * MediaMuxer. Unity sends small RGBA frames (the game's own pixels, top row first) and 16-bit PCM; each pixel is
 * scaled up as it is converted to YUV, so the pixel art stays sharp. Called from Unity's main thread; finish()
 * drains and closes the file on its own thread, and state() reports 0 working, 1 done, -1 failed.
 */
public final class EchoClipEncoder {
    private static final String TAG = "EchoClip";
    private static final long TIMEOUT_US = 10_000;

    private MediaCodec video, audio;
    private MediaMuxer muxer;
    private int videoTrack = -1, audioTrack = -1;
    private boolean muxerStarted, videoEnded, audioEnded;
    private final ArrayList<Pending> pending = new ArrayList<>();
    private final MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();

    private int width, height, scale, outW, outH, fps, sampleRate, channels;
    private long frames, audioFrames;
    private byte[] ys, us, vs, row;
    private volatile int state;

    private static final class Pending {
        final boolean isVideo;
        final byte[] data;
        final MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();

        Pending(boolean isVideo, ByteBuffer buffer, MediaCodec.BufferInfo from) {
            this.isVideo = isVideo;
            data = new byte[from.size];
            buffer.position(from.offset);
            buffer.limit(from.offset + from.size);
            buffer.get(data);
            info.set(0, from.size, from.presentationTimeUs, from.flags);
        }
    }

    /** The scale and frame rate actually used: begin() steps down when the phone's encoder can't do what was asked. */
    public int getScale() { return scale; }
    public int getFps() { return fps; }

    // The first of: what was asked, x3 at that rate, what was asked at 30 fps, x3 at 30 fps, that this phone's H.264
    // encoder says it can do (a 1080p-only encoder can't take the vertical preset's 1152 x 2048).
    private static int[] chooseMode(int width, int height, int scale, int fps) {
        int[][] tries = { { scale, fps }, { Math.min(scale, 3), fps }, { scale, Math.min(fps, 30) }, { Math.min(scale, 3), Math.min(fps, 30) } };
        MediaCodecInfo.VideoCapabilities caps = null;
        try {
            MediaCodec probe = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC);
            caps = probe.getCodecInfo().getCapabilitiesForType(MediaFormat.MIMETYPE_VIDEO_AVC).getVideoCapabilities();
            probe.release();
        } catch (Exception e) {
            Log.w(TAG, "couldn't read the encoder's capabilities", e);
        }
        if (caps != null)
            for (int[] t : tries)
                if (caps.areSizeAndRateSupported(width * t[0], height * t[0], t[1])) return t;
        return tries[tries.length - 1];
    }

    public boolean begin(String path, int width, int height, int scale, int fps, int sampleRate, int channels) {
        try {
            int[] mode = chooseMode(width, height, scale, fps);
            scale = mode[0];
            fps = mode[1];
            this.width = width;
            this.height = height;
            this.scale = scale;
            this.fps = fps;
            this.sampleRate = sampleRate;
            this.channels = channels;
            outW = width * scale;
            outH = height * scale;
            ys = new byte[width * height];
            us = new byte[width * height];
            vs = new byte[width * height];
            row = new byte[outW];
            new File(path).delete();

            MediaFormat vf = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, outW, outH);
            vf.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible);
            // 8 Mbps for the 864 x 1536 30 fps video, more for bigger and smoother (14 Mbps at 1152 x 2048 60 fps).
            long bits = (long) outW * outH * fps / 10;
            vf.setInteger(MediaFormat.KEY_BIT_RATE, (int) Math.max(8_000_000L, Math.min(20_000_000L, bits)));
            vf.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
            vf.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
            if (Build.VERSION.SDK_INT >= 24) {
                vf.setInteger(MediaFormat.KEY_COLOR_STANDARD, MediaFormat.COLOR_STANDARD_BT709);
                vf.setInteger(MediaFormat.KEY_COLOR_RANGE, MediaFormat.COLOR_RANGE_LIMITED);
            }
            video = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC);
            video.configure(vf, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            video.start();

            if (sampleRate > 0 && channels > 0) {
                MediaFormat af = MediaFormat.createAudioFormat(MediaFormat.MIMETYPE_AUDIO_AAC, sampleRate, channels);
                af.setInteger(MediaFormat.KEY_AAC_PROFILE, MediaCodecInfo.CodecProfileLevel.AACObjectLC);
                af.setInteger(MediaFormat.KEY_BIT_RATE, 128_000);
                af.setInteger(MediaFormat.KEY_MAX_INPUT_SIZE, 64 * 1024);
                audio = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_AUDIO_AAC);
                audio.configure(af, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
                audio.start();
            } else {
                audioEnded = true;
            }

            muxer = new MediaMuxer(path, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            state = 0;
            return true;
        } catch (Exception e) {
            Log.w(TAG, "begin failed", e);
            release();
            state = -1;
            return false;
        }
    }

    /** One frame: width x height RGBA, top row first. A frame the encoder has no room for is skipped, not delayed. */
    public void addFrame(byte[] rgba) {
        if (video == null || state != 0) return;
        try {
            long pts = frames * 1_000_000L / fps;
            frames++;
            int index = video.dequeueInputBuffer(TIMEOUT_US * 3);
            if (index >= 0) {
                Image image = video.getInputImage(index);
                if (image != null) {
                    toYuv(rgba);
                    fill(image);
                }
                video.queueInputBuffer(index, 0, outW * outH * 3 / 2, pts, 0);
            }
            drain(video, false);
        } catch (Exception e) {
            fail("frame", e);
        }
    }

    /** 16-bit little-endian PCM, interleaved. */
    public void addAudio(byte[] pcm, int bytes) {
        if (audio == null || state != 0) return;
        try {
            int frameBytes = 2 * channels, offset = 0, tries = 0;
            while (offset < bytes && tries++ < 50) {
                int index = audio.dequeueInputBuffer(TIMEOUT_US);
                if (index < 0) {
                    drain(audio, false);
                    continue;
                }
                ByteBuffer buffer = audio.getInputBuffer(index);
                buffer.clear();
                int n = Math.min(buffer.remaining(), bytes - offset);
                n -= n % frameBytes;
                buffer.put(pcm, offset, n);
                long pts = audioFrames * 1_000_000L / sampleRate;
                audio.queueInputBuffer(index, 0, n, pts, 0);
                audioFrames += n / frameBytes;
                offset += n;
            }
            drain(audio, false);
        } catch (Exception e) {
            fail("audio", e);
        }
    }

    public void finish() {
        if (state != 0 || video == null) {
            state = -1;
            return;
        }
        new Thread(() -> {
            try {
                endOfStream(video, frames * 1_000_000L / fps);
                if (audio != null) endOfStream(audio, audioFrames * 1_000_000L / sampleRate);
                for (int i = 0; i < 1000 && !(videoEnded && audioEnded); i++) {
                    if (!videoEnded) drain(video, true);
                    if (!audioEnded && audio != null) drain(audio, true);
                }
                boolean ok = muxerStarted && videoEnded;
                if (muxerStarted) muxer.stop();
                release();
                state = ok ? 1 : -1;
            } catch (Exception e) {
                Log.w(TAG, "finish failed", e);
                release();
                state = -1;
            }
        }, "EchoClipFinish").start();
    }

    public int state() {
        return state;
    }

    private static void endOfStream(MediaCodec codec, long pts) {
        for (int i = 0; i < 100; i++) {
            int index = codec.dequeueInputBuffer(TIMEOUT_US);
            if (index >= 0) {
                codec.queueInputBuffer(index, 0, 0, pts, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
                return;
            }
        }
    }

    // BT.709 limited range, per source pixel; each is then written scale x scale times.
    private void toYuv(byte[] rgba) {
        for (int i = 0, p = 0; i < ys.length; i++, p += 4) {
            int r = rgba[p] & 0xFF, g = rgba[p + 1] & 0xFF, b = rgba[p + 2] & 0xFF;
            ys[i] = (byte) (16 + ((47 * r + 157 * g + 16 * b + 128) >> 8));
            us[i] = (byte) (128 + ((-26 * r - 87 * g + 112 * b + 128) >> 8));
            vs[i] = (byte) (128 + ((112 * r - 102 * g - 10 * b + 128) >> 8));
        }
    }

    private void fill(Image image) {
        Image.Plane[] planes = image.getPlanes();
        writePlane(planes[0], ys, outW, outH, 1);
        writePlane(planes[1], us, outW / 2, outH / 2, 2);
        writePlane(planes[2], vs, outW / 2, outH / 2, 2);
    }

    // A plane of planeW x planeH samples; each covers step x step output pixels, so (x, y) reads source pixel
    // (x * step / scale, y * step / scale).
    private void writePlane(Image.Plane plane, byte[] src, int planeW, int planeH, int step) {
        ByteBuffer buffer = plane.getBuffer();
        int rowStride = plane.getRowStride(), pixelStride = plane.getPixelStride();
        for (int y = 0; y < planeH; y++) {
            int srcRow = (y * step / scale) * width;
            int base = y * rowStride;
            if (pixelStride == 1) {
                for (int x = 0; x < planeW; x++) row[x] = src[srcRow + x * step / scale];
                buffer.position(base);
                buffer.put(row, 0, planeW);
            } else {
                for (int x = 0; x < planeW; x++) buffer.put(base + x * pixelStride, src[srcRow + x * step / scale]);
            }
        }
    }

    private void drain(MediaCodec codec, boolean ending) {
        if (codec == null) return;
        boolean isVideo = codec == video;
        for (int i = 0; i < 64; i++) {
            int index = codec.dequeueOutputBuffer(info, ending ? TIMEOUT_US : 0);
            if (index == MediaCodec.INFO_TRY_AGAIN_LATER) {
                return;
            } else if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                int track = muxer.addTrack(codec.getOutputFormat());
                if (isVideo) videoTrack = track;
                else audioTrack = track;
                startMuxerWhenReady();
            } else if (index >= 0) {
                ByteBuffer data = codec.getOutputBuffer(index);
                if ((info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0) info.size = 0;
                if (info.size > 0 && data != null) write(isVideo, data, info);
                codec.releaseOutputBuffer(index, false);
                if ((info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                    if (isVideo) videoEnded = true;
                    else audioEnded = true;
                    return;
                }
            }
        }
    }

    private void write(boolean isVideo, ByteBuffer data, MediaCodec.BufferInfo bufferInfo) {
        if (!muxerStarted) {
            pending.add(new Pending(isVideo, data, bufferInfo));
            return;
        }
        data.position(bufferInfo.offset);
        data.limit(bufferInfo.offset + bufferInfo.size);
        muxer.writeSampleData(isVideo ? videoTrack : audioTrack, data, bufferInfo);
    }

    // The muxer can only start once every track's format is known; samples from before then wait in pending.
    private void startMuxerWhenReady() {
        if (muxerStarted || videoTrack < 0 || (audio != null && audioTrack < 0)) return;
        muxer.start();
        muxerStarted = true;
        for (Pending p : pending) {
            ByteBuffer buffer = ByteBuffer.wrap(p.data);
            muxer.writeSampleData(p.isVideo ? videoTrack : audioTrack, buffer, p.info);
        }
        pending.clear();
    }

    private void fail(String where, Exception e) {
        Log.w(TAG, where + " failed", e);
        state = -1;
        release();
    }

    private void release() {
        try { if (video != null) { video.stop(); video.release(); } } catch (Exception ignored) { }
        try { if (audio != null) { audio.stop(); audio.release(); } } catch (Exception ignored) { }
        try { if (muxer != null) muxer.release(); } catch (Exception ignored) { }
        video = null;
        audio = null;
        muxer = null;
    }
}
