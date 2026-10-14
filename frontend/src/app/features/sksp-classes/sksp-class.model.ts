export interface SkspClassAttendee {
  candidateId: string;
  personFullName: string;
  /** true = obecny, false = nieobecny, null = nie zaznaczono (nie liczy się do frekwencji). */
  isPresent: boolean | null;
}

export interface SkspClass {
  id: string;
  year: number;
  classDate: string;
  startTime?: string | null;
  endTime?: string | null;
  topic: string;
  notes: string | null;
  attendees: SkspClassAttendee[];
}

export interface SkspClassFormValue {
  year: number;
  classDate: string;
  startTime?: string;
  endTime?: string;
  topic: string;
  notes?: string;
}

/** „obecni 5/8” – ilu zaznaczono jako obecnych spośród zaznaczonych. */
export function presenceSummary(c: SkspClass): string {
  const marked = c.attendees.filter(a => a.isPresent !== null);
  if (marked.length === 0) return 'obecność nieoznaczona';
  return `obecni ${marked.filter(a => a.isPresent).length}/${c.attendees.length}`;
}
