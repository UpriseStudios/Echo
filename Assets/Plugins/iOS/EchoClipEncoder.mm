// Echo's video clips on iOS (a user revision): H.264 + AAC into an MP4 with AVAssetWriter. Unity sends small RGBA
// frames (the game's own pixels, top row first) and interleaved float audio; each pixel is scaled up as it is copied
// into the BGRA pixel buffer, so the pixel art stays sharp. EchoClip_State: 0 working, 1 done, -1 failed.

#import <AVFoundation/AVFoundation.h>

static AVAssetWriter *gWriter;
static AVAssetWriterInput *gVideo;
static AVAssetWriterInputPixelBufferAdaptor *gAdaptor;
static AVAssetWriterInput *gAudio;
static CMAudioFormatDescriptionRef gAudioFormat;
static int gWidth, gHeight, gScale, gFps, gRate, gChannels;
static long long gFrames, gAudioFrames;
static volatile int gState = -1;

static void EchoClipReset(void)
{
    if (gAudioFormat) { CFRelease(gAudioFormat); gAudioFormat = NULL; }
    gWriter = nil;
    gVideo = nil;
    gAdaptor = nil;
    gAudio = nil;
}

extern "C" bool EchoClip_Begin(const char *path, int width, int height, int scale, int fps, int sampleRate, int channels)
{
    EchoClipReset();
    gWidth = width; gHeight = height; gScale = scale; gFps = fps; gRate = sampleRate; gChannels = channels;
    gFrames = 0; gAudioFrames = 0;
    int outW = width * scale, outH = height * scale;

    NSURL *url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
    [[NSFileManager defaultManager] removeItemAtURL:url error:nil];
    NSError *error = nil;
    gWriter = [[AVAssetWriter alloc] initWithURL:url fileType:AVFileTypeMPEG4 error:&error];
    if (!gWriter) { gState = -1; return false; }

    NSDictionary *videoSettings = @{
        AVVideoCodecKey: AVVideoCodecTypeH264,
        AVVideoWidthKey: @(outW),
        AVVideoHeightKey: @(outH),
        AVVideoCompressionPropertiesKey: @{ AVVideoAverageBitRateKey: @(MAX(8000000L, MIN(20000000L, (long)outW * outH * fps / 10))), AVVideoMaxKeyFrameIntervalKey: @(fps) },
    };
    gVideo = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeVideo outputSettings:videoSettings];
    gVideo.expectsMediaDataInRealTime = YES;
    NSDictionary *pixels = @{
        (id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
        (id)kCVPixelBufferWidthKey: @(outW),
        (id)kCVPixelBufferHeightKey: @(outH),
    };
    gAdaptor = [AVAssetWriterInputPixelBufferAdaptor assetWriterInputPixelBufferAdaptorWithAssetWriterInput:gVideo
                                                                               sourcePixelBufferAttributes:pixels];
    if (![gWriter canAddInput:gVideo]) { gState = -1; return false; }
    [gWriter addInput:gVideo];

    if (sampleRate > 0 && channels > 0) {
        AudioChannelLayout layout = {0};
        layout.mChannelLayoutTag = channels == 1 ? kAudioChannelLayoutTag_Mono : kAudioChannelLayoutTag_Stereo;
        NSDictionary *audioSettings = @{
            AVFormatIDKey: @(kAudioFormatMPEG4AAC),
            AVSampleRateKey: @(sampleRate),
            AVNumberOfChannelsKey: @(channels),
            AVEncoderBitRateKey: @(128000),
            AVChannelLayoutKey: [NSData dataWithBytes:&layout length:sizeof(layout)],
        };
        gAudio = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeAudio outputSettings:audioSettings];
        gAudio.expectsMediaDataInRealTime = YES;
        if ([gWriter canAddInput:gAudio]) [gWriter addInput:gAudio];
        else gAudio = nil;

        AudioStreamBasicDescription pcm = {0};
        pcm.mSampleRate = sampleRate;
        pcm.mFormatID = kAudioFormatLinearPCM;
        pcm.mFormatFlags = kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked;
        pcm.mBytesPerPacket = 4 * channels;
        pcm.mFramesPerPacket = 1;
        pcm.mBytesPerFrame = 4 * channels;
        pcm.mChannelsPerFrame = channels;
        pcm.mBitsPerChannel = 32;
        CMAudioFormatDescriptionCreate(kCFAllocatorDefault, &pcm, sizeof(layout), &layout, 0, NULL, NULL, &gAudioFormat);
    }

    if (![gWriter startWriting]) { gState = -1; return false; }
    [gWriter startSessionAtSourceTime:kCMTimeZero];
    gState = 0;
    return true;
}

// A frame the writer has no room for is skipped, not delayed: its time slot is kept so the clip stays in step.
extern "C" void EchoClip_AddFrame(const unsigned char *rgba)
{
    long long index = gFrames++;
    if (gState != 0 || !gVideo.readyForMoreMediaData || gAdaptor.pixelBufferPool == NULL) return;
    CVPixelBufferRef buffer = NULL;
    if (CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, gAdaptor.pixelBufferPool, &buffer) != kCVReturnSuccess) return;
    CVPixelBufferLockBaseAddress(buffer, 0);
    unsigned char *base = (unsigned char *)CVPixelBufferGetBaseAddress(buffer);
    size_t bytesPerRow = CVPixelBufferGetBytesPerRow(buffer);
    int outW = gWidth * gScale, outH = gHeight * gScale;
    for (int y = 0; y < outH; y++) {
        const unsigned char *src = rgba + (size_t)(y / gScale) * gWidth * 4;
        unsigned char *dst = base + (size_t)y * bytesPerRow;
        for (int x = 0; x < outW; x++) {
            const unsigned char *s = src + (x / gScale) * 4;
            dst[x * 4 + 0] = s[2];
            dst[x * 4 + 1] = s[1];
            dst[x * 4 + 2] = s[0];
            dst[x * 4 + 3] = 255;
        }
    }
    CVPixelBufferUnlockBaseAddress(buffer, 0);
    if (![gAdaptor appendPixelBuffer:buffer withPresentationTime:CMTimeMake(index, gFps)]) gState = -1;
    CVPixelBufferRelease(buffer);
}

extern "C" void EchoClip_AddAudio(const float *samples, int frames)
{
    long long start = gAudioFrames;
    gAudioFrames += frames;
    if (gState != 0 || gAudio == nil || gAudioFormat == NULL || frames <= 0 || !gAudio.readyForMoreMediaData) return;
    size_t bytes = (size_t)frames * 4 * gChannels;
    CMBlockBufferRef block = NULL;
    if (CMBlockBufferCreateWithMemoryBlock(kCFAllocatorDefault, NULL, bytes, kCFAllocatorDefault, NULL, 0, bytes,
                                           kCMBlockBufferAssureMemoryNowFlag, &block) != kCMBlockBufferNoErr) return;
    CMBlockBufferReplaceDataBytes(samples, block, 0, bytes);
    CMSampleBufferRef sample = NULL;
    OSStatus status = CMAudioSampleBufferCreateReadyWithPacketDescriptions(kCFAllocatorDefault, block, gAudioFormat, frames,
                                                                            CMTimeMake(start, gRate), NULL, &sample);
    if (status == noErr && sample) {
        [gAudio appendSampleBuffer:sample];
        CFRelease(sample);
    }
    CFRelease(block);
}

extern "C" void EchoClip_Finish(void)
{
    if (gWriter == nil || gState != 0) { gState = -1; return; }
    [gVideo markAsFinished];
    if (gAudio) [gAudio markAsFinished];
    AVAssetWriter *writer = gWriter;
    [writer finishWritingWithCompletionHandler:^{
        gState = writer.status == AVAssetWriterStatusCompleted ? 1 : -1;
        EchoClipReset();
    }];
}

extern "C" int EchoClip_State(void)
{
    return gState;
}
