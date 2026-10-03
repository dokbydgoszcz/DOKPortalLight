export type CandidateStatus = 'InFormation' | 'Completed' | 'Stopped';

export interface CandidateRetreat {
  year: number;
  isCompleted: boolean;
}

export const romanYear = (year: number): string => ['', 'I', 'II', 'III'][year] ?? String(year);

export interface Candidate {
  id: string;
  personId: string;
  personFullName: string;
  parishName: string | null;
  year: number;
  attendancePercentage: number | null;
  opinionsCollected: number;
  opinionsRequired: number;
  /** Wynika z daty: rok formacji awansuje sam 1 września, po III roku jest „Completed”. */
  status: CandidateStatus;
  isFormationStopped: boolean;
  formationStopNote: string | null;
  retreats: CandidateRetreat[];
}

export interface CandidateFormValue {
  personId: string;
  year: number;
  attendancePercentage?: number;
  opinionsCollected: number;
  /** Rekolekcje po jednym wpisie na rok formacji; brak wpisu = nic nie zaplanowano. */
  retreats: CandidateRetreat[];
  isFormationStopped?: boolean;
  formationStopNote?: string;
}
