export type CandidateStatus = 'InFormation' | 'Completed' | 'Stopped';

export interface CandidateRetreat {
  year: number;
  isCompleted: boolean;
}

export const romanYear = (year: number): string => ['', 'I', 'II', 'III'][year] ?? String(year);

export interface CandidateFormationEvent {
  kind: 'Enrolled' | 'Advanced' | 'Completed' | 'Changed' | 'Stopped' | 'Resumed';
  fromYear: number | null;
  toYear: number | null;
  atUtc: string;
  performedBy: string | null;
  /** Powód zatrzymania formacji. */
  note?: string | null;
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
    case 'Stopped':
      text = event.note ? `Formacja zatrzymana: ${event.note}` : 'Formacja zatrzymana';
      break;
    case 'Resumed':
      text = 'Formacja wznowiona';
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
  /** Frekwencja liczona z list obecności zajęć SKŚP (zamiast ręcznego wpisu). */
  attendanceFromClasses?: boolean;
  classesAttended?: number;
  classesRecorded?: number;
  /** Czego brakuje do ukończenia formacji. */
  completionGaps?: string[];
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
  /** Ukończenie formacji mimo braków (rekolekcje, opinie, frekwencja). */
  confirmIncomplete?: boolean;
}

/** Minimalna frekwencja do ukończenia formacji (tak samo liczy serwer). */
export const MIN_ATTENDANCE = 80;

/** Braki do ukończenia formacji – ta sama reguła co na serwerze (CandidateCompletion). */
export function completionGaps(retreats: CandidateRetreat[], opinionsCollected: number, opinionsRequired: number, attendance: number | null | undefined): string[] {
  const gaps: string[] = [];
  const done = new Set(retreats.filter(r => r.isCompleted).map(r => r.year));
  const missing = [1, 2, 3].filter(y => !done.has(y));
  if (missing.length) gaps.push(`rekolekcje niezaliczone: rok ${missing.map(romanYear).join(', ')}`);
  if (opinionsCollected < opinionsRequired) gaps.push(`opinie: ${opinionsCollected} z ${opinionsRequired}`);
  if (attendance === null || attendance === undefined) gaps.push('brak frekwencji');
  else if (attendance < MIN_ATTENDANCE) gaps.push(`frekwencja ${attendance}% (wymagane ${MIN_ATTENDANCE}%)`);
  return gaps;
}
