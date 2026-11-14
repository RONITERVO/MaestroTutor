// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
/** One native chooser/copy owner, retained until its last worker drains. */
final class FileSelectionGate {
    private static Object owner;
    static synchronized boolean ready(){return owner==null;}
    static synchronized void acquire(Object next){if(owner!=null&&owner!=next)throw new IllegalStateException("Finish or cancel the current file selection first");owner=next;}
    static synchronized void release(Object previous){if(owner==previous)owner=null;}
    private FileSelectionGate(){}
}
