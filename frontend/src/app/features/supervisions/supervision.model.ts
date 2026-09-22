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
