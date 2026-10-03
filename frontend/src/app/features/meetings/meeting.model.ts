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
  isAttended: boolean | null;
  notes: string | null;
  attendees: MeetingAttendee[];
}

export interface CreateMeetingValue {
  dokCaseId?: string;
  groupLabel?: string;
  meetingDate: string;
  isAttended?: boolean;
  notes?: string;
  attendees?: AttendeeValue[];
}
