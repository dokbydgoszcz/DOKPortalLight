import { Attachment } from '../../shared/attachments/attachment.model';

export type Institution = 'SKSP' | 'DOK';

export interface Supervision {
  id: string;
  institution: Institution;
  groupLabel: string;
  supervisionDate: string;
  attendeesCount: number | null;
  expectedCount: number | null;
  topic: string | null;
  conclusion: string | null;
  attachments: Attachment[];
  /** Imienna lista obecności (katechiści z grupy). */
  attendance?: SupervisionAttendee[];
}

export interface SupervisionAttendee {
  personId: string;
  fullName: string;
  isPresent: boolean;
}

/** Grupa superwizyjna z misji i jej katechiści z ważną misją. */
export interface SupervisionGroup {
  name: string;
  members: { personId: string; fullName: string }[];
}

export interface CreateSupervisionValue {
  institution: Institution;
  groupLabel: string;
  supervisionDate: string;
  attendeesCount?: number;
  expectedCount?: number;
  topic?: string;
  conclusion?: string;
  attendance?: { personId: string; isPresent: boolean }[];
}
