export type DokPath = 'BaptismCandidate' | 'Confirmation' | 'Communion' | 'Conversion' | 'ReturnToUnity';
export type DokStage = 'Application' | 'Formation' | 'Sacrament' | 'Graduate';

export interface DokCase {
  id: string;
  personId: string;
  personFullName: string;
  parishName: string | null;
  path: DokPath;
  stage: DokStage;
  catechistPersonId: string;
  catechistFullName: string;
  mentorPersonId: string | null;
  mentorFullName: string | null;
  lastMeetingDate: string | null;
  completedAtUtc: string | null;
}

export interface DokCaseFormValue {
  personId: string;
  path: DokPath;
  stage: DokStage;
  catechistPersonId: string;
  mentorPersonId?: string;
}
