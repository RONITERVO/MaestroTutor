// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import java.io.*;
import java.util.*;
import java.util.function.BooleanSupplier;
import java.util.zip.*;

/** An indexed, private ZIP copy; members are never extracted to their supplied paths. */
final class MotionArchive implements AutoCloseable {
    static final long MAX_BYTES=2L*1024*1024*1024, MAX_EXPANDED=4L*1024*1024*1024;
    static final int MAX_MODELS=1024, MAX_ENTRIES=4096, MAX_DIRECTORY_BYTES=4*1024*1024;
    private final ZipFile zip;
    private final List<ZipEntry> models=new ArrayList<>();
    MotionArchive(File file,BooleanSupplier cancelled) throws IOException {
        checkDirectory(file,cancelled); zip=new ZipFile(file);
        try {
            Set<String> names=new HashSet<>(); Enumeration<? extends ZipEntry> entries=zip.entries();
            while(entries.hasMoreElements()) {
                check(cancelled); ZipEntry entry=entries.nextElement(); String name=entry.getName();
                if(!safePath(name)||!names.add(name))throw invalid("The ZIP contains ambiguous or unsafe entry names. Export it again.");
                if(entry.isDirectory()||name.startsWith("__MACOSX/")||!isModel(name))continue;
                if(entry.getSize()<28||entry.getSize()>SelectedFiles.MAX_FILE_BYTES)throw invalid("Each GLB or VRM inside the ZIP must be between 28 bytes and 64 MB.");
                if(models.size()>=MAX_MODELS)throw invalid("Choose a ZIP with at most 1,024 GLB or VRM files.");
                models.add(entry);
            }
            if(models.isEmpty())throw invalid("This ZIP has no GLB or VRM models. Export self-contained animated models; nested ZIPs are not opened.");
            models.sort(Comparator.comparing(ZipEntry::getName));
        } catch(IOException|RuntimeException ex) {zip.close();throw ex;}
    }
    int count(){return models.size();}
    long bytes(int index){return models.get(index).getSize();}
    String name(int index){String value=models.get(index).getName();return SelectedFiles.safeName(value.substring(value.lastIndexOf('/')+1));}
    android.net.Uri copy(int index,SelectedFiles destination,BooleanSupplier cancelled) throws IOException {
        check(cancelled);ZipEntry member=models.get(index);
        return destination.copyStream(zip.getInputStream(member),name(index),member.getSize(),member.getCrc(),cancelled);
    }
    @Override public void close() throws IOException {zip.close();}
    static boolean isModel(String name){String lower=name.toLowerCase(Locale.ROOT);return lower.endsWith(".glb")||lower.endsWith(".vrm");}
    private static boolean safePath(String name){
        if(name.isEmpty()||name.length()>1024||name.startsWith("/")||name.indexOf('\\')>=0||name.indexOf(':')>=0)return false;
        for(int i=0;i<name.length();i++)if(Character.isISOControl(name.charAt(i)))return false;
        for(String part:name.split("/",-1))if(part.equals(".")||part.equals(".."))return false;
        return true;
    }
    static SelectedFiles.SelectionException invalid(String text){return new SelectedFiles.SelectionException(text);}
    static void check(BooleanSupplier cancelled)throws InterruptedIOException{if(cancelled.getAsBoolean()||Thread.currentThread().isInterrupted())throw new InterruptedIOException("ZIP reading cancelled");}
    private static int u16(byte[] b,int offset){return(b[offset]&255)|((b[offset+1]&255)<<8);}
    private static long u32(byte[] b,int offset){return u16(b,offset)|((long)u16(b,offset+2)<<16);}
    // Bound the central directory before ZipFile can allocate it. These exports
    // do not need ZIP64, split volumes, encryption or arbitrary compression codecs.
    static void checkDirectory(File file,BooleanSupplier cancelled)throws IOException{
        try(RandomAccessFile input=new RandomAccessFile(file,"r")){
            long length=input.length();if(length<22||length>MAX_BYTES)throw invalid("Choose a ZIP of 2 GB or smaller.");
            byte[] tail=new byte[(int)Math.min(length,65557)];input.seek(length-tail.length);input.readFully(tail);int end=-1;
            for(int i=tail.length-22;i>=0;i--)if(u32(tail,i)==0x06054b50L&&i+22+u16(tail,i+20)==tail.length){end=i;break;}
            if(end<0)throw invalid("This ZIP is incomplete. Download it again.");
            int count=u16(tail,end+10);long bytes=u32(tail,end+12),start=u32(tail,end+16),limit=length-tail.length+end;
            if(u16(tail,end+4)!=0||u16(tail,end+6)!=0||u16(tail,end+8)!=count||count<1||count>MAX_ENTRIES||bytes>MAX_DIRECTORY_BYTES||start+bytes!=limit)
                throw invalid("Use a standard single ZIP with at most 4,096 entries and 4 MB of directory metadata; ZIP64 is not supported.");
            input.seek(start);byte[] header=new byte[46];long expanded=0;
            for(int i=0;i<count;i++){
                check(cancelled);if(input.getFilePointer()+46>limit)throw invalid("The ZIP directory is incomplete.");input.readFully(header);
                int flags=u16(header,8),method=u16(header,10),nameBytes=u16(header,28);long size=u32(header,24),compressed=u32(header,20);
                long next=input.getFilePointer()+nameBytes+u16(header,30)+u16(header,32);
                if(u32(header,0)!=0x02014b50L||(flags&1)!=0||(method!=0&&method!=8)||u16(header,34)!=0||nameBytes<1||nameBytes>4096||next>limit||u32(header,42)>=start||compressed>MAX_BYTES||size==0xffffffffL)
                    throw invalid("This ZIP uses an unsupported or damaged entry. Use an unencrypted standard ZIP export.");
                expanded+=size;if(expanded>MAX_EXPANDED)throw invalid("The ZIP expands beyond 4 GB. Split the export into smaller collections.");input.seek(next);
            }
            if(input.getFilePointer()!=limit)throw invalid("The ZIP directory does not match its entry count.");
        }
    }
}
