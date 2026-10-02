// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
 public interface IProgramClock {DateTimeOffset UtcNow {get;} TimeZoneInfo LocalZone {get;}}
 public interface IProgramClockWorld {IProgramClock Clock {get;}}
 public sealed class SystemProgramClock:IProgramClock {
  public static readonly SystemProgramClock Instance=new();
  public DateTimeOffset UtcNow=>DateTimeOffset.UtcNow;
  public TimeZoneInfo LocalZone=>TimeZoneInfo.Local;
 }
 // One private, bounded event wait. A new weekly wait chooses the next occurrence;
 // no queue of past occurrences, background alarms, saved execution or auto-start.
 public sealed class CalendarSubscription:IProgramEventWatch
 {
  public const float SampleInterval=.1f;
  readonly IProgramClock clock;readonly DateTimeOffset due;readonly string zone,missed;readonly double grace;
  float nextSample;bool disposed;
  static readonly string[] DayNames={"sun","mon","tue","wed","thu","fri","sat"};
  static JObject Field(JObject schema,string title){schema["title"]=title;return schema;}
  static JObject Kind(string name){var s=Choice(name);s["x-static"]=true;return s;}
  static JObject Instant()=>Text(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?(Z|[+-][0-9]{2}:[0-9]{2})$",40);
  static JObject Common(string kind)=>new(){["kind"]=Kind(kind),["missed"]=Field(Choice("fail","report"),"If the deadline is missed"),["graceSeconds"]=Field(Number(0,3600),"Allowed lateness (seconds)")};
  static JObject Input(){
   var once=Common("once");once["at"]=Field(Instant(),"Date and time with UTC offset");
   var weekly=Common("weekly");weekly["hour"]=Field(Number(0,23,true),"Hour");weekly["minute"]=Field(Number(0,59,true),"Minute");weekly["weekdays"]=Field(List(Choice(DayNames),1,7),"Days of the week");weekly["zone"]=Field(Choice("device","utc"),"Time zone");weekly["ambiguous"]=Field(Choice("earlier","later"),"Repeated daylight-saving time");
   var a=Object(once);a["title"]="One date and time";var b=Object(weekly);b["title"]="Weekly time";
   return new JObject{["type"]="object",["title"]="Schedule",["x-features"]=new JArray("calendarSchedules.v1"),["x-discriminators"]=new JArray("kind"),["oneOf"]=new JArray(a,b)};
  }
  public static BehaviourCatalog.EventDefinition Definition()=>new("clock.scheduled","Scheduled time reached",
   "Wait for one calendar occurrence while this program runs. Source must be empty. once requires an ISO date/time with Z or an explicit UTC offset; no implicit local parsing. weekly chooses the next strictly future selected weekday/hour/minute when the wait begins, in UTC or the device zone reported then. That zone is captured for this wait. Duplicate weekdays are invalid. Nonexistent daylight-saving times are skipped; repeated times use the explicit earlier/later choice. Re-enter the wait for another occurrence; missed weeks never build a backlog. Polling is at most 10 times/second. The device wall clock controls deadlines: moving it forward can make a deadline late, moving it back delays it. Lateness beyond graceSeconds either fails the program (missed=fail) or emits value late with late=true (report); otherwise value is due. lateSeconds is capped at one million and lateSecondsCapped reports that cap; exact scheduledUtc/observedUtc remain available. A past once deadline uses the same policy. Pause, Stop and reload cancel the wait without resumption or background notifications. A delivered event only wakes code; it does not itself run room actions.",
   Object(new JObject{["scheduledUtc"]=Text("^.{1,40}$",40),["observedUtc"]=Text("^.{1,40}$",40),["lateSeconds"]=Number(0,1000000),["lateSecondsCapped"]=new JObject{["type"]="boolean"},["late"]=new JObject{["type"]="boolean"},["zone"]=Text("^.{1,128}$",128)}),
   input:Input(),example:new JObject{["kind"]="weekly",["missed"]="fail",["graceSeconds"]=60,["hour"]=18,["minute"]=0,["weekdays"]=new JArray("mon","tue","wed","thu","fri"),["zone"]="device",["ambiguous"]="earlier"},
   watch:(world,args,now)=>new CalendarSubscription(world,args,now));
  public static string Stamp(DateTimeOffset value)=>value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",CultureInfo.InvariantCulture);
  public static BehaviourCatalog.FactDefinition Fact()=>new("clock.now",ProgramDataType.Read(JObject.Parse("{\"record\":{\"utc\":\"text\",\"local\":\"text\",\"zone\":\"text\",\"offsetMinutes\":\"number\",\"weekday\":\"text\"}}")),"Current calendar time",
   "The current device clock: exact UTC and local ISO timestamps, runtime-reported local zone, UTC offset in minutes and local weekday (sun..sat). It can change with device clock/zone settings; it is not a monotonic timer, a trusted server time or permission to start a program. Calendar timestamps are text, avoiding numeric program range/precision loss.",null,null,(context,args)=>{
    var clock=(context.World as IProgramClockWorld)?.Clock;if(clock==null)return null;var zone=clock.LocalZone;var utc=clock.UtcNow;var local=TimeZoneInfo.ConvertTime(utc,zone);
    return ProgramValue.Literal(new JObject{["utc"]=Stamp(utc),["local"]=local.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffzzz",CultureInfo.InvariantCulture),["zone"]=zone.Id,["offsetMinutes"]=local.Offset.TotalMinutes,["weekday"]=DayNames[(int)local.DayOfWeek]});
   });
  public CalendarSubscription(IProgramEventWorld world,JObject args,float now){
   clock=(world as IProgramClockWorld)?.Clock??throw new ProgramFault("Calendar time is unavailable in this room");missed=(string)args["missed"];grace=(double)args["graceSeconds"];nextSample=now+SampleInterval;
   if((string)args["kind"]=="once"){
    string[] formats={"yyyy-MM-dd'T'HH:mm:ss'Z'","yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'","yyyy-MM-dd'T'HH:mm:sszzz","yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"};
    if(!DateTimeOffset.TryParseExact((string)args["at"],formats,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out due))throw new ProgramFault("Choose a real calendar date and time with Z or an explicit UTC offset");zone="UTC";
   }else{
    var tz=(string)args["zone"]=="utc"?TimeZoneInfo.Utc:clock.LocalZone;zone=tz.Id;due=NextWeekly(clock.UtcNow,tz,args);
   }
  }
  static DateTimeOffset NextWeekly(DateTimeOffset now,TimeZoneInfo zone,JObject args){
   var days=((JArray)args["weekdays"]).Values<string>().ToArray();if(days.Distinct().Count()!=days.Length)throw new ProgramFault("Choose each weekday at most once");
   var date=TimeZoneInfo.ConvertTime(now,zone).Date;
   try{for(int day=0;day<=14;day++){
    var local=date.AddDays(day).AddHours((int)args["hour"]).AddMinutes((int)args["minute"]);if(!days.Contains(DayNames[(int)local.DayOfWeek])||zone.IsInvalidTime(local))continue;
    TimeSpan offset=zone.IsAmbiguousTime(local)?((string)args["ambiguous"]=="earlier"?zone.GetAmbiguousTimeOffsets(local).Max():zone.GetAmbiguousTimeOffsets(local).Min()):zone.GetUtcOffset(local);
    var candidate=new DateTimeOffset(local,offset).ToUniversalTime();if(candidate>now)return candidate;
   }}catch(ArgumentOutOfRangeException){throw new ProgramFault("The next scheduled date is outside the supported calendar");}
   throw new ProgramFault("No valid selected calendar occurrence was found");
  }
  public bool Poll(float now,out ProgramValue value,out JObject fields,out string error){
   value=default;fields=null;error=null;if(disposed||now<nextSample)return false;nextSample=now+SampleInterval;
   var observed=clock.UtcNow;if(observed<due)return false;double seconds=(observed-due).TotalSeconds;bool late=seconds>grace;
   if(late&&missed=="fail"){error="The calendar deadline was missed; inspect the schedule before starting again";return false;}
   value=new ProgramValue(late?"late":"due");fields=new JObject{["scheduledUtc"]=Stamp(due),["observedUtc"]=Stamp(observed),["lateSeconds"]=Math.Min(1000000,seconds),["lateSecondsCapped"]=seconds>1000000,["late"]=late,["zone"]=zone};return true;
  }
  public void Dispose(){disposed=true;}
 }
}
