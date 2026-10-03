# Calendar schedules in shared programs

`clock.scheduled@1` is a native catalog event subscription. Its one-off and weekly
variants use the existing awaitEvent block, shared interpreter, bounded watch
slots, typed fields, book editor and app-agent program. There is no extra model
connection, timer service or bytecode format. `calendarSchedules.v1` gates it.

## One date and time

```json
{"kind":"once","at":"2026-10-02T18:00:00+03:00","missed":"fail","graceSeconds":60}
```

Use an ISO date/time with `Z` or a numeric UTC offset. A missing offset is rejected;
a calendar date such as February 30 is rejected by native preparation. Fractional
seconds up to seven digits are accepted. No locale-dependent date guessing occurs.
A deadline already in the past uses the same explicit lateness policy as one
crossed during the wait.

## Weekly time

```json
{"kind":"weekly","hour":18,"minute":0,"weekdays":["mon","tue","wed","thu","fri"],"zone":"device","ambiguous":"earlier","missed":"report","graceSeconds":60}
```

A wait chooses the next strictly future selected weekday/hour/minute. Re-entering
it after delivery chooses another future occurrence; it does not accumulate a
backlog. Days use sun/mon/tue/wed/thu/fri/sat and must be distinct. Hour and minute
are integers. `zone` is device or utc; the runtime-reported device time zone is
captured when this wait starts. A later wait reads it again. Changing device zone
while waiting does not silently reinterpret that already chosen deadline.

Nonexistent local times during a daylight-saving transition are skipped for that
date. Repeated local times use `ambiguous:earlier|later`, choosing one occurrence,
not both. A loop at the chosen occurrence goes to the next selected date.

## Delivery and lifecycle

Sampling is bounded to ten checks per second. Delivery uses the device wall clock:
a forward clock adjustment can make a deadline late; a backward adjustment delays
it. Use session sleep/time.wait when elapsed time is the requirement instead.

`graceSeconds` (0–3600) is the tolerated lateness. Beyond it, `missed:fail` fails
without executing subsequent program blocks; `report` delivers value `late`.
Within it, the value is `due`. Typed fields are scheduledUtc, observedUtc, late,
lateSeconds, lateSecondsCapped and zone. Lateness is capped at 1,000,000 seconds to
respect program numeric limits; the cap flag and exact timestamps preserve the
meaning of a much older deadline. A program can branch on late and choose whether
to act. Receiving the event does not prove any later action succeeded.

The existing awaitEvent timeout is still monotonic session time. Source must be
empty. Inputs are evaluated when the wait begins and stay fixed; selectors cannot
be computed. Stop, app pause/focus loss, program edits, room replacement and reload
cancel the wait under existing lifecycle rules. Returning focus does not resume it.
This is active-app scheduling, without Android background alarms, notifications,
durable program execution or automatic launch. Those require separate policies and
acceptance, and must not be implied when confirming a user's request.

`clock.now` reports UTC/local ISO timestamps, local zone, offsetMinutes and weekday
through the same fact path seen by agent and human. Timestamps are text to avoid
numeric-range or precision loss. Device time is not trusted server time.

## Verification

EditMode checks offset parsing, invalid dates, forward/backward wall-clock changes,
late failure/reporting and capped values, disposal, next-occurrence/no-backlog
selection, DST gaps and repeated times, variant bindings and clock facts.
PlayMode saves the shared program through the real room editor, injects a calendar
clock, creates a real native ball only on an on-time occurrence, branches on a
missed occurrence, and verifies Stop/pause without replay. Tests advance the injected
clock; they do not change the PC clock or claim a multiweek physical-device test.
The book edits that exact program and replays its native observations; browser
save acknowledgements are simulated. Quest timezone, lifecycle, readability and
performance acceptance remain outstanding.
