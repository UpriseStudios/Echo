package com.uprise.echo;

import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import android.util.Log;

import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * Saves a clip to the phone's gallery (a user revision: clips kept as well as shared). Videos go to Movies/Echo and
 * GIFs to Pictures/Echo through MediaStore, which needs no storage permission from Android 10 (API 29). Older phones
 * return false and the game offers the share sheet instead.
 */
public final class EchoGallery {
    private static final String TAG = "EchoGallery";

    private EchoGallery() { }

    public static boolean save(Context context, String path, String mime, String name) {
        if (Build.VERSION.SDK_INT < 29) return false;
        ContentResolver resolver = context.getContentResolver();
        boolean video = mime.startsWith("video/");
        ContentValues values = new ContentValues();
        values.put(MediaStore.MediaColumns.DISPLAY_NAME, name);
        values.put(MediaStore.MediaColumns.MIME_TYPE, mime);
        values.put(MediaStore.MediaColumns.RELATIVE_PATH, (video ? Environment.DIRECTORY_MOVIES : Environment.DIRECTORY_PICTURES) + "/Echo");
        values.put(MediaStore.MediaColumns.IS_PENDING, 1);
        Uri collection = video
            ? MediaStore.Video.Media.getContentUri(MediaStore.VOLUME_EXTERNAL_PRIMARY)
            : MediaStore.Images.Media.getContentUri(MediaStore.VOLUME_EXTERNAL_PRIMARY);
        Uri item = resolver.insert(collection, values);
        if (item == null) return false;
        try (InputStream in = new FileInputStream(path); OutputStream out = resolver.openOutputStream(item)) {
            byte[] buffer = new byte[64 * 1024];
            int n;
            while ((n = in.read(buffer)) > 0) out.write(buffer, 0, n);
        } catch (Exception e) {
            Log.w(TAG, "save failed: " + e);
            resolver.delete(item, null, null);
            return false;
        }
        values.clear();
        values.put(MediaStore.MediaColumns.IS_PENDING, 0);
        resolver.update(item, values, null, null);
        return true;
    }
}
