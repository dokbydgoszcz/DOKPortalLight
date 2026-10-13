export type DokPath = 'BaptismCandidate' | 'Confirmation' | 'Communion' | 'Conversion' | 'ReturnToUnity';
export type DokStage = 'Evangelization' | 'CloserFormation' | 'Prekatechumenate' | 'Catechumenate' | 'Election' | 'Neophyte' | 'Graduate';

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
  meetingsRecorded: number;
  meetingsAttended: number;
  /** Data przerwania formacji (rezygnacja itp.); brak = sprawa nie jest przerwana. */
  interruptedOn?: string | null;
  interruptionReason?: string | null;
}

/** Filtr listy: w toku, przerwane, wszystkie (''). */
export type DokCaseStatusFilter = 'Active' | 'Interrupted' | '';

export interface DokCaseEvent {
  kind: string;
  description: string;
  atUtc: string;
  performedByEmail: string | null;
}

export interface DokCaseFormValue {
  personId: string;
  path: DokPath;
  stage: DokStage;
  catechistPersonId: string;
  mentorPersonId?: string;
}
