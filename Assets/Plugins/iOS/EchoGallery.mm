// Saves a clip to the Photos library (a user revision: clips kept as well as shared). Add-only access: iOS asks the
// player once, with Info.plist's NSPhotoLibraryAddUsageDescription (Editor/IosPhotoLibrary). The result is polled by
// EchoGallery_Status: 0 while saving, 1 saved, -1 refused or failed.
#import <Photos/Photos.h>

static volatile int sEchoGalleryStatus = 0;

extern "C" void EchoGallery_Save(const char* path, int video)
{
    sEchoGalleryStatus = 0;
    NSURL* url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
    [[PHPhotoLibrary sharedPhotoLibrary] performChanges:^{
        if (video) [PHAssetChangeRequest creationRequestForAssetFromVideoAtFileURL:url];
        else [PHAssetChangeRequest creationRequestForAssetFromImageAtFileURL:url];
    } completionHandler:^(BOOL success, NSError* error) {
        sEchoGalleryStatus = success ? 1 : -1;
    }];
}

extern "C" int EchoGallery_Status()
{
    return sEchoGalleryStatus;
}
