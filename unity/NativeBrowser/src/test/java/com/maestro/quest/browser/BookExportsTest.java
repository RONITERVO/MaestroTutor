// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import android.os.Looper;
import android.webkit.ValueCallback;
import org.json.JSONObject;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.*;
import static org.junit.Assert.*;
import static org.robolectric.Shadows.shadowOf;

@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class BookExportsTest {
    static class Queue extends AbstractExecutorService {
        final Deque<Runnable> tasks=new ArrayDeque<>();boolean stopped;
        public void execute(Runnable r){assertFalse(stopped);tasks.add(r);}
        void drain(){while(!tasks.isEmpty())tasks.remove().run();}
        public void shutdown(){stopped=true;} public List<Runnable> shutdownNow(){stopped=true;return new ArrayList<>(tasks);}
        public boolean isShutdown(){return stopped;} public boolean isTerminated(){return stopped&&tasks.isEmpty();}public boolean awaitTermination(long n,TimeUnit unit){return isTerminated();}
    }
    static class Host implements BookExports.Host {
        boolean active=true;ValueCallback<String> poll;int acks,polls;
        public boolean active(){return active;}
        public void evaluate(String script,ValueCallback<String> result){if(script.contains("fileExportPoll")){poll=result;polls++;}else{acks++;result.onReceiveValue("true");}}
        void deliver(String raw){ValueCallback<String> p=poll;poll=null;p.onReceiveValue(JSONObject.quote(raw));}
    }
    @Test public void documentReplacementSuppressesWorkerAckAndCleansPendingWrite() throws Exception {
        Host host=new Host();Queue worker=new Queue();BookExportSessionTest.Sink sink=new BookExportSessionTest.Sink();BookExports broker=new BookExports(host,(name,mime)->sink,worker);
        broker.poll();broker.poll();assertEquals(1,host.polls);host.deliver(BookExportSessionTest.open());assertEquals(0,sink.writes);broker.close();host.active=false;worker.drain();shadowOf(Looper.getMainLooper()).idle();assertEquals(0,host.acks);assertEquals(1,sink.aborts);assertTrue(worker.isTerminated());
    }
    @Test public void idleTimeoutDiscardsPendingFileWithoutDisablingLaterExports() throws Exception {
        Host host=new Host();Queue worker=new Queue();BookExportSessionTest.Sink sink=new BookExportSessionTest.Sink();BookExports broker=new BookExports(host,(name,mime)->sink,worker);
        broker.poll();host.deliver(BookExportSessionTest.open());worker.drain();shadowOf(Looper.getMainLooper()).idle();assertEquals(1,host.acks);
        shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMillis(25));host.deliver("null");shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(61));worker.drain();assertEquals(1,sink.aborts);
        broker.poll();host.deliver(new JSONObject(BookExportSessionTest.open()).put("id",BookExportSessionTest.OTHER).toString());worker.drain();shadowOf(Looper.getMainLooper()).idle();assertEquals(2,host.acks);broker.close();worker.drain();
    }
    @Test public void stalePagePollCannotStartExportAfterItsDeadline() throws Exception {
        Host host=new Host();Queue worker=new Queue();BookExportSessionTest.Sink sink=new BookExportSessionTest.Sink();BookExports broker=new BookExports(host,(name,mime)->sink,worker);
        broker.poll();ValueCallback<String> old=host.poll;shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(11));broker.poll();assertEquals(2,host.polls);
        old.onReceiveValue(JSONObject.quote(BookExportSessionTest.open()));assertTrue(worker.tasks.isEmpty());
        host.deliver(BookExportSessionTest.open());worker.drain();shadowOf(Looper.getMainLooper()).idle();assertEquals(1,host.acks);broker.close();worker.drain();
    }
}
