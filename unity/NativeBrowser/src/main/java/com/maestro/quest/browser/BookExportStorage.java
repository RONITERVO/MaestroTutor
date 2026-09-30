// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.content.ContentResolver;
import android.content.ContentValues;
import android.database.Cursor;
import android.net.Uri;
import android.os.Environment;
import android.provider.MediaStore;
import java.io.IOException;
import java.io.OutputStream;

/** App-owned Downloads: hidden until fully closed, deleted on abort. API 29+. */
final class BookExportStorage implements BookExportSession.Storage {
    private final ContentResolver resolver;
    BookExportStorage(ContentResolver resolver) { this.resolver = resolver; }
    public BookExportSession.Sink open(String name, String mime) throws IOException {
        ContentValues values = new ContentValues();
        values.put(MediaStore.MediaColumns.DISPLAY_NAME,name);
        values.put(MediaStore.MediaColumns.MIME_TYPE,mime);
        values.put(MediaStore.MediaColumns.RELATIVE_PATH,Environment.DIRECTORY_DOWNLOADS + "/Maestro/");
        values.put(MediaStore.MediaColumns.IS_PENDING,1);
        // Android calculates DATE_EXPIRES for pending rows; it is read-only.
        Uri uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI,values);
        if (uri == null) throw new IOException("Downloads could not create the file.");
        try {
            OutputStream stream = resolver.openOutputStream(uri,"w");
            if (stream == null) throw new IOException("Downloads could not open the file.");
            return new BookExportSession.Sink() {
                private boolean published;
                private OutputStream output = stream;
                public void write(byte[] data) throws IOException { if (output == null) throw new IOException("Export closed"); output.write(data); }
                public String finish() throws IOException {
                    if (output == null) throw new IOException("Export closed");
                    output.flush(); output.close(); output = null;
                    String actualName;
                    try (Cursor cursor = resolver.query(uri,new String[] {MediaStore.MediaColumns.DISPLAY_NAME},null,null,null)) {
                        if (cursor == null || !cursor.moveToFirst()) throw new IOException("Downloads did not return the saved filename.");
                        actualName = cursor.getString(0);
                    }
                    if (!BookExportSession.validName(actualName)) throw new IOException("Unexpected export name");
                    ContentValues done = new ContentValues(); done.put(MediaStore.MediaColumns.IS_PENDING,0);
                    if (resolver.update(uri,done,null,null) != 1) throw new IOException("Downloads did not confirm the file.");
                    published = true;
                    return "Downloads/Maestro/" + actualName;
                }
                public void abort() throws IOException {
                    try { if (output != null) output.close(); }
                    finally { output = null; if (!published) resolver.delete(uri,null,null); }
                }
            };
        } catch (Exception failure) {
            resolver.delete(uri,null,null);
            throw new IOException("Downloads could not open the file.",failure);
        }
    }
}
