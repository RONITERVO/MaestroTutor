// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation
{
    // Detached edits shared by physical authoring and named capability calls.
    // The room document validates the complete resulting motion before one save/Undo.
    public static class RoomMotionEdits
    {
        public static bool Put(RoomMotion current,MotionFrame[] frames,bool replace,out RoomMotion result,out string error)
        {
            result=null;error="Provide distinct finite frame times";
            if(frames==null||frames.Length==0||frames.Any(f=>f==null||!float.IsFinite(f.time))||frames.Select(f=>f.time).Distinct().Count()!=frames.Length)return false;
            var old=replace?Array.Empty<MotionFrame>():current?.frames??Array.Empty<MotionFrame>();
            result=new RoomMotion {loop=current?.loop??false,frames=old.Where(f=>!frames.Any(n=>n.time==f.time)).Concat(frames).OrderBy(f=>f.time).Select(f=>f.Copy()).ToArray()};error=null;return true;
        }
        public static bool Remove(RoomMotion current,float[] times,out RoomMotion result,out string error)
        {
            result=null;error="Choose distinct existing frame times";
            if(current==null||times==null||times.Length==0||times.Distinct().Count()!=times.Length||times.Any(t=>!current.frames.Any(f=>f.time==t)))return false;
            var frames=current.frames.Where(f=>!times.Contains(f.time)).Select(f=>f.Copy()).ToArray();
            if(frames.Length>0){float start=frames[0].time;foreach(var frame in frames)frame.time-=start;result=new RoomMotion {loop=current.loop,frames=frames};}error=null;return true;
        }
        public static bool Settings(RoomMotion current,bool? loop,float? duration,out RoomMotion result,out string error)
        {
            result=null;error="Save frames first; duration changes need at least two frames and 0.1–30 seconds";
            if(current==null||current.frames.Length==0||(!loop.HasValue&&!duration.HasValue)||duration.HasValue&&(!float.IsFinite(duration.Value)||duration<.1f||duration>RoomMotion.MaximumSeconds||current.frames.Length<2||current.Duration<=0))return false;
            result=current.Copy();if(loop.HasValue)result.loop=loop.Value;
            if(duration.HasValue){float ratio=duration.Value/current.Duration;foreach(var frame in result.frames)frame.time*=ratio;result.frames[^1].time=duration.Value;}
            error=null;return true;
        }
    }
}
