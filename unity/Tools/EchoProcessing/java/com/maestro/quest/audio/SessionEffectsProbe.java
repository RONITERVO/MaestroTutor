// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.audio;


public final class SessionEffectsProbe {
    private static int cases, assertions;
    private static void check(boolean ok, String message) { ++assertions; if (!ok) throw new AssertionError(message); }
    private static void done(String name) { ++cases; System.out.println("{\"case\":\""+name+"\",\"status\":\"passed\"}"); }
    private static final class Effect implements SessionEffects.Effect {
        boolean enabled = true, controlled = true, enableOk = true, ignoreEnable, throwQuery, throwRelease, throwWatch;
        int releases, sets; SessionEffects.Change controlEvent, enabledEvent;
        @Override public boolean hasControl() { if (throwQuery) throw new IllegalStateException(); return controlled; }
        @Override public boolean isEnabled() { if (throwQuery) throw new IllegalStateException(); return enabled; }
        @Override public boolean setEnabled(boolean wanted) { ++sets; if (!ignoreEnable) enabled = wanted; return enableOk; }
        @Override public void watch(SessionEffects.Change control, SessionEffects.Change state) {
            controlEvent = control; enabledEvent = state; if (throwWatch) throw new IllegalStateException();
        }
        @Override public void release() { ++releases; if (throwRelease) throw new IllegalStateException(); }
    }
    private static final class Backend implements SessionEffects.Backend {
        Effect[] effects = {new Effect(),new Effect(),new Effect()};
        boolean[] available = {true,true,true}; int calls, creates, lastSession, throwAt = -1;
        @Override public boolean available(SessionEffects.Kind kind) { ++calls; if (kind.ordinal() == throwAt) throw new IllegalStateException(); return available[kind.ordinal()]; }
        @Override public SessionEffects.Effect create(SessionEffects.Kind kind,int session) { ++creates; lastSession = session; return effects[kind.ordinal()]; }
        SessionEffects open() { return SessionEffects.open(this,37); }
        void closed(int count) { for (int i=0;i<count;++i) check(effects[i].releases == 1,"release once: "+i); }
    }
    public static void main(String[] args) throws Exception {
        {Backend b = new Backend(); SessionEffects s = b.open(); check(s.isHealthy(),"ready");
        check(s.sessionId()==37 && b.lastSession==37 && b.creates==3,"exact session");
        check(s.queriedMask()==7 && s.availableMask()==7 && s.attachedMask()==7 && s.initiallyEnabledMask()==7,"initial diagnostics");
        check(b.effects[0].enabled && !b.effects[1].enabled && !b.effects[2].enabled,"AEC only, disable reported NS/AGC");
        s.close();s.close();b.closed(3);check(!s.isHealthy(),"closed");} done("owned-prepare-close");
        {for(int id:new int[]{0,-1,-44}) {Backend b=new Backend();SessionEffects s=SessionEffects.open(b,id);check(!s.isHealthy()&&b.calls==0&&b.creates==0,"reject invalid session before side effects");}}
        done("invalid-sessions");
        {Backend b=new Backend(); b.available[0]=false;SessionEffects s=b.open();check(!s.isHealthy()&&s.fault()==SessionEffects.Fault.UNAVAILABLE&&s.queriedMask()==1&&b.creates==0,"no raw fallback");}done("aec-unavailable");
        {Backend b=new Backend();b.available[1]=false;b.available[2]=false;SessionEffects s=b.open();check(s.isHealthy()&&s.availableMask()==1&&b.creates==1,"optional effects absent");s.close();b.closed(1);}done("only-aec-available");
        for(int index=0;index<3;++index){Backend b=new Backend();b.effects[index]=null;SessionEffects s=b.open();check(!s.isHealthy()&&s.fault()==SessionEffects.Fault.CREATE,"creation failure");s.close();b.closed(index);}done("partial-create-rollback");
        for(int index=0;index<3;++index){Backend b=new Backend();b.effects[index].controlled=false;SessionEffects s=b.open();check(!s.isHealthy()&&s.fault()==SessionEffects.Fault.CONTROL,"no control");check(b.effects[index].sets==0,"do not mutate someone else's effect");s.close();b.closed(index+1);}done("partial-control-rollback");
        for(int index=0;index<3;++index){Backend b=new Backend();b.effects[index].enableOk=false;SessionEffects s=b.open();check(!s.isHealthy()&&s.fault()==SessionEffects.Fault.ENABLE,"enable failure");s.close();b.closed(index+1);}done("partial-enable-rollback");
        {Backend b=new Backend();b.effects[1].ignoreEnable=true;SessionEffects s=b.open();check(!s.isHealthy(),"read back enabled state");s.close();b.closed(2);}done("false-enable-success");
        for(int index=0;index<3;++index){Backend b=new Backend();b.effects[index].throwWatch=true;SessionEffects s=b.open();check(!s.isHealthy()&&s.fault()==SessionEffects.Fault.QUERY,"listener registration failure");s.close();b.closed(index+1);}done("listener-failure-rollback");
        for(int index=0;index<3;++index){Backend b=new Backend();b.throwAt=index;SessionEffects s=b.open();check(!s.isHealthy(),"availability throws");s.close();b.closed(index);}done("availability-failure-rollback");
        for(int index=0;index<3;++index){Backend b=new Backend();SessionEffects s=b.open();b.effects[index].controlled=false;check(!s.isHealthy(),"poll detects control loss");b.effects[index].controlled=true;check(!s.isHealthy(),"loss latches");s.close();b.closed(3);}done("poll-control-loss-latched");
        for(int index=0;index<3;++index){Backend b=new Backend();SessionEffects s=b.open();b.effects[index].enabled=!b.effects[index].enabled;check(!s.isHealthy(),"poll detects change");s.close();b.closed(3);}done("poll-enabled-change");
        for(int index=0;index<3;++index){Backend b=new Backend();SessionEffects s=b.open();b.effects[index].controlEvent.changed(false);b.effects[index].controlEvent.changed(true);check(!s.isHealthy(),"transient control loss cannot heal");s.close();b.closed(3);}done("callback-control-loss");
        for(int index=0;index<3;++index){Backend b=new Backend();SessionEffects s=b.open();b.effects[index].enabledEvent.changed(index!=0);b.effects[index].enabledEvent.changed(index==0);check(!s.isHealthy(),"transient state change cannot heal");s.close();b.closed(3);}done("callback-enabled-change");
        {Backend b=new Backend();SessionEffects s=b.open();for(int i=0;i<3;++i){b.effects[i].controlEvent.changed(true);b.effects[i].enabledEvent.changed(i==0);}check(s.isHealthy(),"matching callbacks are benign");s.close();}done("benign-callbacks");
        {Backend b=new Backend();SessionEffects s=b.open();b.effects[1].throwQuery=true;check(!s.isHealthy()&&s.fault()==SessionEffects.Fault.QUERY,"query failure");s.close();b.closed(3);}done("health-query-failure");
        {Backend b=new Backend();SessionEffects s=b.open();for(Effect effect:b.effects)effect.throwRelease=true;s.close();s.close();b.closed(3);check(!s.isHealthy(),"continue releasing all effects");}done("release-exceptions");
        {Backend b=new Backend();SessionEffects old=b.open();old.close();Backend fresh=new Backend();SessionEffects next=fresh.open();b.effects[0].controlEvent.changed(false);check(next.isHealthy()&&!old.isHealthy(),"late callback isolated");next.close();}done("late-callback-isolation");
        {Backend b=new Backend();SessionEffects s=b.open();Thread callback=new Thread(()->b.effects[0].controlEvent.changed(false));callback.start();callback.join();check(!s.isHealthy(),"cross-thread event visible");s.close();}done("callback-thread-visibility");
        {Backend b=new Backend();SessionEffects s=b.open();Thread callback=new Thread(()->{for(int i=0;i<10000;++i)b.effects[0].enabledEvent.changed(false);});callback.start();s.close();callback.join();s.close();b.closed(3);check(!s.isHealthy(),"close races only invalidation");}done("callback-close-race");
        System.out.println("{\"status\":\"passed\",\"sessionEffectsCases\":"+cases+",\"assertions\":"+assertions+",\"deviceOpened\":false,\"physicalAudio\":false}");
    }
}
