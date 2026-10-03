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
}

export interface CreateSupervisionValue {
  institution: Institution;
  groupLabel: string;
  supervisionDate: string;
  attendeesCount?: number;
  expectedCount?: number;
  topic?: string;
  conclusion?: string;
}
