export interface Meeting {
  id: string;
  dokCaseId: string | null;
  caseLabel: string | null;
  groupLabel: string | null;
  meetingDate: string;
  isAttended: boolean | null;
  notes: string | null;
}

export interface CreateMeetingValue {
  dokCaseId?: string;
  groupLabel?: string;
  meetingDate: string;
  isAttended?: boolean;
  notes?: string;
}
