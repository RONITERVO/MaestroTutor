// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.Binder;
import android.os.ParcelFileDescriptor;
import android.os.Process;
import android.provider.OpenableColumns;
import java.io.FileNotFoundException;

/** The WebView can read selected copies; this provider exports no directory or write API. */
public final class SelectedFileProvider extends ContentProvider {
    @Override public boolean onCreate() { return true; }
    private SelectedFiles.Entry entry(Uri uri) {
        if (Binder.getCallingUid() != Process.myUid() || uri == null || uri.getPathSegments().size() != 1 || !(getContext().getPackageName()+".maestro.selected").equals(uri.getAuthority())) throw new SecurityException("Unavailable selected file");
        SelectedFiles.Entry result = SelectedFiles.ENTRIES.get(uri.getLastPathSegment());
        if (result == null) throw new IllegalArgumentException("Selected file expired");
        return result;
    }
    @Override public String getType(Uri uri) { return entry(uri).mime; }
    @Override public Cursor query(Uri uri,String[] projection,String selection,String[] args,String order) {
        SelectedFiles.Entry value = entry(uri);
        String[] columns = projection == null ? new String[] { OpenableColumns.DISPLAY_NAME,OpenableColumns.SIZE } : projection;
        MatrixCursor cursor = new MatrixCursor(columns,1); Object[] row = new Object[columns.length];
        for (int i = 0; i < columns.length; i++) row[i] = OpenableColumns.DISPLAY_NAME.equals(columns[i]) ? value.name : OpenableColumns.SIZE.equals(columns[i]) ? value.file.length() : null;
        cursor.addRow(row); return cursor;
    }
    @Override public ParcelFileDescriptor openFile(Uri uri,String mode) throws FileNotFoundException {
        if (!"r".equals(mode)) throw new SecurityException("Selected files are read-only");
        return ParcelFileDescriptor.open(entry(uri).file,ParcelFileDescriptor.MODE_READ_ONLY);
    }
    @Override public Uri insert(Uri uri,ContentValues values) { throw new UnsupportedOperationException(); }
    @Override public int delete(Uri uri,String selection,String[] args) { throw new UnsupportedOperationException(); }
    @Override public int update(Uri uri,ContentValues values,String selection,String[] args) { throw new UnsupportedOperationException(); }
}
