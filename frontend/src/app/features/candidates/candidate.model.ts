export interface Candidate {
  id: string;
  personId: string;
  personFullName: string;
  parishName: string | null;
  year: number;
  attendancePercentage: number | null;
  opinionsCollected: number;
  opinionsRequired: number;
  isRetreatCompleted: boolean;
}

export interface CandidateFormValue {
  personId: string;
  year: number;
  attendancePercentage?: number;
  opinionsCollected: number;
  isRetreatCompleted: boolean;
}
