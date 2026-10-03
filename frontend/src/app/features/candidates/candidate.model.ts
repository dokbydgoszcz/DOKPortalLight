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
  retreats: CandidateRetreat[];
}

export interface CandidateFormValue {
  personId: string;
  year: number;
  attendancePercentage?: number;
  opinionsCollected: number;
  /** Rekolekcje po jednym wpisie na rok formacji; brak wpisu = nic nie zaplanowano. */
  retreats: CandidateRetreat[];
}
