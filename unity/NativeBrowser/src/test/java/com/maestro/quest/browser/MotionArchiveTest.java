// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.app.Activity;
import android.net.Uri;
import java.io.*;
import java.nio.file.Files;
import java.util.*;
import java.util.zip.*;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.Robolectric;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import static org.junit.Assert.*;

@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class MotionArchiveTest {
    Activity activity;File file;
    @Before public void setup()throws Exception{activity=Robolectric.buildActivity(Activity.class).setup().get();file=File.createTempFile("motion-archive-",".zip",activity.getCacheDir());}
    @After public void cleanup(){file.delete();activity.finish();}
    static void writeZip(File file,int count,boolean stored)throws IOException{
        try(ZipOutputStream zip=new ZipOutputStream(new FileOutputStream(file))){
            for(int i=0;i<count;i++){byte[] bytes=new byte[32];Arrays.fill(bytes,(byte)i);ZipEntry entry=new ZipEntry("character/motion-"+String.format(Locale.ROOT,"%04d",i)+".glb");
                if(stored){CRC32 crc=new CRC32();crc.update(bytes);entry.setMethod(ZipEntry.STORED);entry.setSize(bytes.length);entry.setCrc(crc.getValue());}
                zip.putNextEntry(entry);zip.write(bytes);zip.closeEntry();}
        }
    }
    static int central(byte[] bytes){for(int i=0;i<bytes.length-4;i++)if(bytes[i]==0x50&&bytes[i+1]==0x4b&&bytes[i+2]==1&&bytes[i+3]==2)return i;throw new AssertionError();}
    @Test public void crcAndSizeCorruptionNeverGrantPartialFiles()throws Exception{
        writeZip(file,1,true);byte[] bytes=Files.readAllBytes(file.toPath());int cd=central(bytes);bytes[cd+16]^=1;Files.write(file.toPath(),bytes);
        int before=SelectedFiles.ENTRIES.size();try(MotionArchive archive=new MotionArchive(file,()->false);SelectedFiles cache=new SelectedFiles(activity)){
            assertThrows(IOException.class,()->archive.copy(0,cache,()->false));assertEquals(before,SelectedFiles.ENTRIES.size());
        }
        writeZip(file,1,true);bytes=Files.readAllBytes(file.toPath());cd=central(bytes);bytes[cd+24]=31;Files.write(file.toPath(),bytes);
        try(MotionArchive archive=new MotionArchive(file,()->false);SelectedFiles cache=new SelectedFiles(activity)){assertThrows(IOException.class,()->archive.copy(0,cache,()->false));assertEquals(before,SelectedFiles.ENTRIES.size());}
    }
    @Test public void rejectsUnsafePathsDuplicatesNestedOnlyArchivesAndOversizedModels()throws Exception{
        for(String path:new String[]{"../outside.glb","/absolute.glb","folder/../model.glb","C:/model.glb","nested.zip"}){
            try(ZipOutputStream zip=new ZipOutputStream(new FileOutputStream(file))){zip.putNextEntry(new ZipEntry(path));zip.write(new byte[32]);zip.closeEntry();}
            assertThrows(IOException.class,()->new MotionArchive(file,()->false));
        }
        writeZip(file,1,true);byte[] bytes=Files.readAllBytes(file.toPath());int cd=central(bytes);bytes[cd+24]=1;bytes[cd+27]=4;Files.write(file.toPath(),bytes);
        assertThrows(IOException.class,()->new MotionArchive(file,()->false));
        writeZip(file,2,true);bytes=Files.readAllBytes(file.toPath());cd=central(bytes);int second=cd+46+"character/motion-0000.glb".length();bytes[second+46+"character/motion-000".length()]='0';Files.write(file.toPath(),bytes);
        assertThrows(IOException.class,()->new MotionArchive(file,()->false));
    }
    @Test public void boundsDirectoryBeforeZipFileAllocationAndRejectsExcessModels()throws Exception{
        writeZip(file,1,false);byte[] bytes=Files.readAllBytes(file.toPath());int end=bytes.length-22;bytes[end+15]=127;Files.write(file.toPath(),bytes);
        assertThrows(IOException.class,()->new MotionArchive(file,()->false));
        writeZip(file,1025,false);assertThrows(IOException.class,()->new MotionArchive(file,()->false));
        writeZip(file,1024,false);try(MotionArchive archive=new MotionArchive(file,()->false)){assertEquals(1024,archive.count());}
    }
    @Test public void skipsDocumentationAndMacMetadataAndSupportsStoredAndDeflatedMembers()throws Exception{
        for(boolean stored:new boolean[]{true,false}){
            writeZip(file,2,stored);byte[] original=Files.readAllBytes(file.toPath());
            try(MotionArchive archive=new MotionArchive(file,()->false);SelectedFiles cache=new SelectedFiles(activity)){
                Uri uri=archive.copy(1,cache,()->false);byte[] actual=Files.readAllBytes(SelectedFiles.ENTRIES.get(uri.getLastPathSegment()).file.toPath());assertEquals(32,actual.length);for(byte value:actual)assertEquals(1,value);
            }assertArrayEquals(original,Files.readAllBytes(file.toPath()));
        }
        try(ZipOutputStream zip=new ZipOutputStream(new FileOutputStream(file))){for(String path:new String[]{"readme.txt","__MACOSX/._model.glb","model.glb"}){zip.putNextEntry(new ZipEntry(path));zip.write(new byte[32]);zip.closeEntry();}}
        try(MotionArchive archive=new MotionArchive(file,()->false)){assertEquals(1,archive.count());}
    }
    @Test public void cancellationBeforeListingOrReadingDoesNotGrantAMember()throws Exception{
        writeZip(file,1,false);assertThrows(InterruptedIOException.class,()->new MotionArchive(file,()->true));int before=SelectedFiles.ENTRIES.size();
        try(MotionArchive archive=new MotionArchive(file,()->false);SelectedFiles cache=new SelectedFiles(activity)){
            assertThrows(InterruptedIOException.class,()->archive.copy(0,cache,()->true));assertEquals(before,SelectedFiles.ENTRIES.size());
        }
    }
    @Test public void explicitlySelectedPrivateArchiveProducesExpectedVerifiedModelHashes()throws Exception{
        String path=System.getenv("MAESTRO_TEST_MOTION_ZIP"),expected=System.getenv("MAESTRO_TEST_MOTION_HASHES");Assume.assumeTrue(path!=null&&expected!=null);
        Set<String> hashes=new TreeSet<>();try(MotionArchive archive=new MotionArchive(new File(path),()->false)){
            for(int i=0;i<archive.count();i++)try(SelectedFiles cache=new SelectedFiles(activity)){
                Uri uri=archive.copy(i,cache,()->false);byte[] bytes=Files.readAllBytes(SelectedFiles.ENTRIES.get(uri.getLastPathSegment()).file.toPath());
                hashes.add(java.util.HexFormat.of().formatHex(java.security.MessageDigest.getInstance("SHA-256").digest(bytes)));
            }
        }assertEquals(new TreeSet<>(Arrays.asList(expected.split(","))),hashes);
    }
}
