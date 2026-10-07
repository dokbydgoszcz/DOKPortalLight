import { describe, expect, it } from 'vitest';
import { CandidateFormationEvent, eventText, nextYearLabel } from './candidate.model';

const event = (over: Partial<CandidateFormationEvent>): CandidateFormationEvent => ({
  kind: 'Advanced', fromYear: 1, toYear: 2, atUtc: '2026-10-07T10:00:00Z', performedBy: 'dyrektor@example.org', ...over
});

describe('eventText', () => {
  it('describes each kind of change in Polish, with who did it', () => {
    expect(eventText(event({ kind: 'Enrolled', fromYear: null, toYear: 2 }))).toBe('Wpisany do II roku · dyrektor@example.org');
    expect(eventText(event({}))).toBe('Przeniesiony z I do II roku · dyrektor@example.org');
    expect(eventText(event({ kind: 'Completed', fromYear: 3, toYear: null }))).toBe('Ukończył formację · dyrektor@example.org');
    expect(eventText(event({ kind: 'Changed', fromYear: 1, toYear: 3 }))).toBe('Zmiana roku z I na III · dyrektor@example.org');
  });

  it('leaves out the author when it is unknown', () => {
    expect(eventText(event({ performedBy: null }))).toBe('Przeniesiony z I do II roku');
  });
});

describe('nextYearLabel', () => {
  it('names the move that the button performs', () => {
    expect(nextYearLabel(1)).toBe('Przenieś do II roku');
    expect(nextYearLabel(2)).toBe('Przenieś do III roku');
    expect(nextYearLabel(3)).toBe('Ukończył formację');
  });
});
