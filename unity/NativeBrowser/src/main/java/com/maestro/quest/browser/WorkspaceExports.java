// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.content.Context;
import java.io.*;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.util.Arrays;

/** Native-only, bounded binary publication. Never exposed to WebView JavaScript.
 * Unity supplies only a finished file from this private directory, on a worker thread. */
public final class WorkspaceExports {
    static final long MAX_BYTES = 512L * 1024 * 1024;
    private WorkspaceExports() { }
    public static String Directory(Context context) throws IOException {
        File cache = context.getCacheDir().getCanonicalFile();
        File root = new File(cache,"maestro-workspace-export");
        if (!root.isDirectory() && !root.mkdirs()) throw new IOException("Archive cache is unavailable.");
        if (!root.equals(root.getCanonicalFile())) throw new IOException("Linked archive cache is unsupported.");
        return root.getAbsolutePath();
    }
    public static String Publish(Context context, String source) throws IOException {
        return publish(new File(Directory(context)),new File(source),new BookExportStorage(context.getContentResolver()));
    }
    static String publish(File root, File source, BookExportSession.Storage storage) throws IOException {
        File canonical = source.getCanonicalFile();
        if (!root.equals(root.getCanonicalFile()) || !source.isAbsolute() || !source.equals(canonical) || !root.equals(canonical.getParentFile()) ||
            !source.getName().matches("maestro-workspace-[a-f0-9]{32}\\.zip") || !Files.isRegularFile(source.toPath(),LinkOption.NOFOLLOW_LINKS))
            throw new IOException("Only a privately captured workspace can be exported.");
        long bytes = source.length();
        if (bytes <= 0 || bytes > MAX_BYTES) throw new IOException("Workspace archive exceeds the export limit.");
        BookExportSession.Sink sink = storage.open(source.getName(),"application/zip");
        boolean published = false;
        try {
            try (InputStream input = new FileInputStream(source)) { copy(input,bytes,sink); }
            String location = sink.finish(); published = true; return location;
        } finally {
            if (!published) sink.abort();
        }
    }
    static void copy(InputStream input, long expected, BookExportSession.Sink sink) throws IOException {
        long total = 0; byte[] buffer = new byte[65536]; int count;
        while ((count = input.read(buffer)) != -1) {
            if (count == 0) continue;
            total += count;
            if (total > expected || total > MAX_BYTES) throw new IOException("The archive changed while exporting.");
            sink.write(count == buffer.length ? buffer : Arrays.copyOf(buffer,count));
        }
        if (total != expected) throw new IOException("The archive changed while exporting.");
    }
}
