export type CandidateStatus = 'InFormation' | 'Completed' | 'Stopped';

export interface CandidateRetreat {
  year: number;
  isCompleted: boolean;
}

export const romanYear = (year: number): string => ['', 'I', 'II', 'III'][year] ?? String(year);

export interface CandidateFormationEvent {
  kind: 'Enrolled' | 'Advanced' | 'Completed' | 'Changed';
  fromYear: number | null;
  toYear: number | null;
  atUtc: string;
  performedBy: string | null;
}

export interface AdvanceResult {
  advanced: number;
  completed: number;
  skipped: { candidateId: string; personFullName: string | null; reason: string }[];
}

/** Co robi przycisk przeniesienia kandydata z danego roku. */
export const nextYearLabel = (year: number): string => (year >= 3 ? 'Ukończył formację' : `Przenieś do ${romanYear(year + 1)} roku`);

/** Opis zmiany roku w historii kandydata. */
export function eventText(event: CandidateFormationEvent): string {
  let text: string;
  switch (event.kind) {
    case 'Enrolled':
      text = `Wpisany do ${romanYear(event.toYear ?? 0)} roku`;
      break;
    case 'Advanced':
      text = `Przeniesiony z ${romanYear(event.fromYear ?? 0)} do ${romanYear(event.toYear ?? 0)} roku`;
      break;
    case 'Completed':
      text = 'Ukończył formację';
      break;
    default:
      text = `Zmiana roku z ${romanYear(event.fromYear ?? 0)} na ${romanYear(event.toYear ?? 0)}`;
  }
  return event.performedBy ? `${text} · ${event.performedBy}` : text;
}

export interface Candidate {
  id: string;
  personId: string;
  personFullName: string;
  parishName: string | null;
  year: number;
  attendancePercentage: number | null;
  opinionsCollected: number;
  opinionsRequired: number;
  /** Rok formacji zmienia się tylko ręcznie; po ukończeniu III roku status to „Completed”. */
  status: CandidateStatus;
  isFormationCompleted: boolean;
  /** Od kiedy kandydat jest w obecnym roku (przy ukończeniu: od ukończenia formacji). */
  yearSinceUtc: string;
  /** Historia zmian roku, od najnowszej. */
  events: CandidateFormationEvent[];
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
  isFormationCompleted?: boolean;
  isFormationStopped?: boolean;
  formationStopNote?: string;
}
