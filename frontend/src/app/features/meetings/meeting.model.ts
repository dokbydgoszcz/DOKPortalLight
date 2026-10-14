export interface MeetingAttendee {
  dokCaseId: string;
  personFullName: string;
  isAttended: boolean | null;
}

export interface AttendeeValue {
  dokCaseId: string;
  isAttended?: boolean;
}

export interface Meeting {
  id: string;
  dokCaseId: string | null;
  caseLabel: string | null;
  groupLabel: string | null;
  meetingDate: string;
  /** „18:00:00” z serwera; brak = spotkanie bez godziny. */
  startTime?: string | null;
  endTime?: string | null;
  isAttended: boolean | null;
  notes: string | null;
  attendees: MeetingAttendee[];
  /** Wspólne id spotkań założonych jako seria „co tydzień”. */
  seriesId?: string | null;
}

export interface CreateMeetingValue {
  dokCaseId?: string;
  groupLabel?: string;
  meetingDate: string;
  startTime?: string;
  endTime?: string;
  isAttended?: boolean;
  notes?: string;
  attendees?: AttendeeValue[];
  /** Tylko przy dodawaniu: seria co tydzień do tej daty włącznie. */
  repeatWeeklyUntil?: string;
}

/** Godzina bez sekund („18:00:00” → „18:00”). */
export function hhmm(time: string | null | undefined): string {
  return time ? time.slice(0, 5) : '';
}

/** „18:00–20:00”, „18:00” albo pusto, gdy spotkanie nie ma godziny. */
export function timeRange(meeting: Pick<Meeting, 'startTime' | 'endTime'>): string {
  const start = hhmm(meeting.startTime);
  const end = hhmm(meeting.endTime);
  return start ? (end ? `${start}–${end}` : start) : '';
}

/** Ile spotkań da seria co tydzień od `from` do `until` włącznie (0 przy złych datach). */
export function weeklyCount(from: string, until: string | undefined): number {
  if (!from || !until) return 0;
  const days = (Date.parse(until) - Date.parse(from)) / 86_400_000;
  return days > 0 ? Math.floor(days / 7) + 1 : 0;
}

/** Domyślne godziny nowego spotkania. */
export const DEFAULT_START = '18:00';
export const DEFAULT_END = '20:00';
