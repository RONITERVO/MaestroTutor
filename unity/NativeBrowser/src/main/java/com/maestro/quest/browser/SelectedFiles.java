// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.content.Context;
import android.content.pm.ProviderInfo;
import android.database.Cursor;
import android.net.Uri;
import android.provider.OpenableColumns;
import android.content.res.AssetFileDescriptor;
import android.os.CancellationSignal;
import java.io.*;
import java.util.*;
import java.util.concurrent.ConcurrentHashMap;

/** Copies only user-selected provider streams into a bounded, read-only session cache. */
final class SelectedFiles implements AutoCloseable {
    static final long MAX_FILE_BYTES = 64L * 1024 * 1024, MAX_SESSION_BYTES = 128L * 1024 * 1024;
    static final int MAX_FILES = 8;
    static final Map<String,Entry> ENTRIES = new ConcurrentHashMap<>();
    private static final Set<String> ACTIVE_DIRECTORIES = new HashSet<>();
    static final class SelectionException extends IOException { SelectionException(String message) { super(message); } }
    static final class Entry {
        final File file;
        final String name, mime;
        Entry(File file, String name, String mime) { this.file = file; this.name = name; this.mime = mime; }
    }
    private final Context context;
    private final File directory;
    private final Set<String> keys = new HashSet<>();
    private final long fileLimit, sessionLimit;
    private long used;
    private java.util.function.BooleanSupplier stopped=()->false;
    private volatile boolean closed;
    private volatile InputStream reading;
    private volatile CancellationSignal cancellation;
    SelectedFiles(Context context) throws IOException { this(context,MAX_FILE_BYTES,MAX_SESSION_BYTES); }
    SelectedFiles(Context context, long fileLimit, long sessionLimit) throws IOException {
        this.context = context.getApplicationContext(); this.fileLimit = fileLimit; this.sessionLimit = sessionLimit;
        File parent = new File(context.getCacheDir(),"maestro-selected");
        directory = new File(parent,UUID.randomUUID().toString());
        synchronized (ACTIVE_DIRECTORIES) {
            File[] previous = parent.listFiles();
            if (previous != null) for (File old : previous) {
                if (!old.isDirectory() || !old.getName().matches("[a-f0-9-]{36}") || ACTIVE_DIRECTORIES.contains(old.getCanonicalPath()) || !old.getCanonicalFile().getParentFile().equals(parent.getCanonicalFile())) continue;
                File[] children = old.listFiles();
                if (children != null) for (File child : children) if (child.isFile() && child.getCanonicalFile().getParentFile().equals(old.getCanonicalFile())) child.delete();
                old.delete();
            }
            if (!directory.mkdirs()) throw new IOException("Could not prepare selected files");
            ACTIVE_DIRECTORIES.add(directory.getCanonicalPath());
        }
    }
    static boolean allowedUri(Context context, Uri uri) {
        if (uri == null || !"content".equals(uri.getScheme()) || uri.getAuthority() == null || uri.getUserInfo() != null) return false;
        ProviderInfo provider = context.getPackageManager().resolveContentProvider(uri.getAuthority(),0);
        // A picker must never hand the web app its own settings, keys or database.
        return provider != null && provider.applicationInfo != null && provider.applicationInfo.uid != context.getApplicationInfo().uid;
    }
    synchronized Uri[] copy(Uri[] sources) throws IOException {return copy(sources,()->false);}
    synchronized Uri[] copy(Uri[] sources,java.util.function.BooleanSupplier stopped) throws IOException {
        this.stopped=stopped;
        if (closed || sources == null || sources.length == 0 || sources.length > MAX_FILES) throw new SelectionException("Choose between one and eight files");
        cancellation = new CancellationSignal();
        List<String> added = new ArrayList<>();
        long batchBytes = 0;
        try {
            for (Uri source : new LinkedHashSet<>(Arrays.asList(sources))) {
                checkCancelled();
                if (!allowedUri(context,source)) throw new SelectionException("The selected file source is unavailable");
                String name = "selected-file";
                try (Cursor cursor = context.getContentResolver().query(source,new String[] { OpenableColumns.DISPLAY_NAME,OpenableColumns.SIZE },null,null,null,cancellation)) {
                    if (cursor != null && cursor.moveToFirst()) {
                        int nameColumn = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME), sizeColumn = cursor.getColumnIndex(OpenableColumns.SIZE);
                        if (nameColumn >= 0 && !cursor.isNull(nameColumn)) name = safeName(cursor.getString(nameColumn));
                        if (sizeColumn >= 0 && !cursor.isNull(sizeColumn) && cursor.getLong(sizeColumn) > fileLimit) throw new SelectionException("Each file must be " + (fileLimit / (1024 * 1024)) + " MB or smaller");
                    }
                }
                String mime = context.getContentResolver().getType(source);
                if (mime == null || mime.length() > 128 || !mime.matches("[a-zA-Z0-9.+_-]+/[a-zA-Z0-9.+_-]+")) mime = "application/octet-stream";
                String key = UUID.randomUUID().toString(); File file = new File(directory,key);
                long bytes = 0;
                try (AssetFileDescriptor descriptor = context.getContentResolver().openAssetFileDescriptor(source,"r",cancellation)) {
                    if (descriptor == null) throw new IOException("The selected file could not be opened");
                    try (InputStream input = descriptor.createInputStream(); OutputStream output = new FileOutputStream(file)) {
                    reading = input; checkCancelled();
                    byte[] buffer = new byte[16384]; int count;
                    while ((count = input.read(buffer)) != -1) {
                        checkCancelled(); bytes += count;
                        if (bytes > fileLimit || used + batchBytes + bytes > sessionLimit) throw new SelectionException("Selected files exceed the available file budget; reopen the book to clear it");
                        output.write(buffer,0,count);
                    }
                    }
                } catch (IOException | RuntimeException failure) { file.delete(); throw failure; }
                finally { reading = null; }
                ENTRIES.put(key,new Entry(file,name,mime)); added.add(key); batchBytes += bytes;
            }
            checkCancelled(); keys.addAll(added); used += batchBytes;
            return added.stream().map(key -> new Uri.Builder().scheme("content").authority(context.getPackageName()+".maestro.selected").appendPath(key).build()).toArray(Uri[]::new);
        } catch (IOException | RuntimeException failure) { for (String key : added) remove(key); throw failure; }
        finally { cancellation = null; }
    }
    /** Writes one ZIP member to an opaque owned path, with actual size and CRC checks. */
    synchronized Uri copyStream(InputStream input,String name,long expectedBytes,long expectedCrc,java.util.function.BooleanSupplier cancelled) throws IOException {
        String key=UUID.randomUUID().toString();File file=new File(directory,key);boolean accepted=false;
        try(InputStream source=input) {
            reading=source;checkCancelled();MotionArchive.check(cancelled);
            if(keys.size()>=MAX_FILES||expectedBytes<0||expectedBytes>fileLimit||used+expectedBytes>sessionLimit)throw new SelectionException("The ZIP member exceeds the available file budget");
            java.util.zip.CRC32 crc=new java.util.zip.CRC32();long bytes=0;
            try(OutputStream output=new FileOutputStream(file)){
                byte[] buffer=new byte[16384];int count;
                while((count=source.read(buffer))!=-1){
                    checkCancelled();MotionArchive.check(cancelled);bytes+=count;
                    if(bytes>expectedBytes)throw new SelectionException("The ZIP member is larger than its declared size");
                    crc.update(buffer,0,count);output.write(buffer,0,count);
                }
            }
            checkCancelled();MotionArchive.check(cancelled);
            if(bytes!=expectedBytes||crc.getValue()!=expectedCrc)throw new SelectionException("This ZIP member is damaged. Download the export again");
            ENTRIES.put(key,new Entry(file,safeName(name),"model/gltf-binary"));keys.add(key);used+=bytes;accepted=true;
            return new Uri.Builder().scheme("content").authority(context.getPackageName()+".maestro.selected").appendPath(key).build();
        } finally {reading=null;if(!accepted)file.delete();}
    }
    private void checkCancelled() throws IOException { MotionArchive.check(stopped); if (closed || Thread.currentThread().isInterrupted()) throw new InterruptedIOException("File selection was cancelled"); }
    void cancelCopy() {
        final InputStream input = reading; final CancellationSignal signal = cancellation;
        if (input == null && signal == null) return;
        Thread cancel = new Thread(() -> { if (signal != null) signal.cancel(); if (input != null) try { input.close(); } catch (IOException ignored) { } },"Cancel Maestro file read");
        cancel.setDaemon(true); cancel.start();
    }
    static String safeName(String name) {
        if (name == null) return "selected-file";
        String result = name.replaceAll("[^\\p{L}\\p{N} ._()\\-]","_").trim();
        if (result.isEmpty() || result.equals(".") || result.equals("..")) return "selected-file";
        return result.substring(0,Math.min(result.length(),120));
    }
    private static void remove(String key) { Entry entry = ENTRIES.remove(key); if (entry != null) entry.file.delete(); }
    synchronized void release(Uri[] values) {
        if (values == null) return;
        for (Uri value : values) { String key = value.getLastPathSegment(); if (keys.remove(key)) { Entry entry = ENTRIES.get(key); if (entry != null) used -= entry.file.length(); remove(key); } }
    }
    @Override public void close() {
        closed = true; cancelCopy();
        synchronized (this) { for (String key : keys) remove(key); keys.clear(); directory.delete(); synchronized (ACTIVE_DIRECTORIES) { ACTIVE_DIRECTORIES.remove(directory.getAbsolutePath()); } }
    }
}
