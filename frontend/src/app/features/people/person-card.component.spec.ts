import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { PersonCardComponent } from './person-card.component';
import { api, setup, textOf } from '../../testing/test-helpers';

const person = {
  id: 'p1', firstName: 'Ewa', lastName: 'Maj', fullName: 'Ewa Maj', email: 'ewa@example.org', phone: null, birthDate: null,
  parishId: null, parishName: 'Fara', notes: null, nameDayMonth: null, nameDayDay: null,
  functions: [{ id: 'f1', type: 'Catechist', parishId: null, parishName: null, institutedOn: null, notes: null }]
};

function boot(body: object | null, status = 200) {
  const ctx = setup(PersonCardComponent, {
    providers: [{ provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'p1' }) } } }]
  });
  ctx.fixture.detectChanges();
  const req = ctx.http.expectOne(api('/api/people/p1/overview'));
  if (status === 200) req.flush(body); else req.flush('x', { status, statusText: 'Error' });
  ctx.fixture.detectChanges();
  return ctx;
}

describe('PersonCardComponent', () => {
  it('shows every relation the viewer may see in one place', () => {
    const { el } = boot({
      person,
      dokCases: [],
      casesLed: [{ id: 'c1', personFullName: 'Jan Kowalski', path: 'Bierzmowanie', stage: 'Ewangelizacja', catechistFullName: 'Ewa Maj', role: 'katechista', interruptedOn: null, completedAtUtc: null }],
      candidate: { id: 'k1', formationYear: 3, status: 'ukończył formację', attendancePercentage: 90, opinionsCollected: 2, opinionsRequired: 2 },
      candidateVisible: true,
      missions: [{ id: 'm1', servicePlace: 'Fara', missionStartDate: '2026-01-01', missionEndDate: '2027-01-01', supervisionGroup: 'Grupa A', status: 'ważna' }],
      parishAssignments: [], formatorFunctions: [], documents: [{ id: 'd1', title: 'Wniosek o misję kanoniczną', createdAtUtc: '2026-10-01T10:00:00Z' }],
      supervisions: { present: 3, listed: 4, lastPresentOn: '2026-10-10' },
      account: { email: 'ewa@example.org', roles: ['KatechistaProwadzacy'], isActive: true },
      accountVisible: true
    });

    const text = textOf(el);
    expect(text).toContain('Ewa Maj');
    expect(text).toContain('Katechista');
    expect(text).toContain('Jan Kowalski');
    expect(text).toContain('ukończył formację');
    expect(text).toContain('grupa: Grupa A');
    expect(text).toContain('Wniosek o misję kanoniczną');
    expect(text).toContain('3/4');
    expect(text).toContain('Katechista prowadzący');
    expect(text).toContain('Powiązań: 5');
    expect(TestBed.inject(ActivatedRoute)).toBeTruthy();
  });

  it('hides sections the viewer has no access to', () => {
    const { el } = boot({
      person, dokCases: null, casesLed: null, candidate: null, candidateVisible: false, missions: null, parishAssignments: null,
      formatorFunctions: null, documents: null, supervisions: null, account: null, accountVisible: false
    });

    const text = textOf(el);
    expect(text).not.toContain('Konto w portalu');
    expect(text).not.toContain('Misje kanoniczne');
    expect(text).not.toContain('SKŚP – kandydatura');
    expect(text).toContain('Brak powiązań');
  });

  it('says when the person does not exist', () => {
    const { el } = boot(null, 404);
    expect(textOf(el)).toContain('Nie znaleziono osoby.');
  });
  it('shows assignments, formator role, notes, an interrupted case and a disabled account', () => {
    const { el } = boot({
      person: { ...person, notes: 'Uwaga testowa', functions: [], email: null },
      dokCases: [
        { id: 'c1', personFullName: 'Ewa Maj', path: 'Bierzmowanie', stage: 'Ewangelizacja', catechistFullName: 'Anna', role: 'podopieczny', interruptedOn: '2026-10-01', completedAtUtc: null },
        { id: 'c2', personFullName: 'Ewa Maj', path: 'Eucharystia', stage: 'Absolwent', catechistFullName: 'Anna', role: 'podopieczny', interruptedOn: null, completedAtUtc: '2026-06-01T00:00:00Z' }
      ],
      casesLed: [{ id: 'c3', personFullName: 'Jan', path: 'Bierzmowanie', stage: 'Ewangelizacja', catechistFullName: 'X', role: 'mentor', interruptedOn: '2026-10-01', completedAtUtc: null }],
      candidate: null, candidateVisible: true,
      missions: [], parishAssignments: [{ parishName: 'Fara', needDescription: 'Katecheza dorosłych', assignedAtUtc: '2026-09-01T00:00:00Z' }],
      formatorFunctions: ['Wykładowca'], documents: [],
      supervisions: { present: 0, listed: 2, lastPresentOn: null },
      account: { email: 'ewa@example.org', roles: [], isActive: false }, accountVisible: true
    });

    const text = textOf(el);
    expect(text).toContain('Uwaga testowa');
    expect(text).toContain('Wykładowca');
    expect(text).toContain('Katecheza dorosłych');
    expect(text).toContain('przerwana 01.10.2026');
    expect(text).toContain('ukończona 01.06.2026');
    expect(text).toContain('mentor');
    expect(text).toContain('wyłączone');
    expect(text).toContain('Nie jest kandydatem.');
    expect(text).toContain('Brak misji.');
    expect(text).toContain('Brak pism.');
  });

  it('shows a generic error when the card cannot be loaded', () => {
    const { el } = boot(null, 500);
    expect(textOf(el)).toContain('Nie udało się wczytać karty osoby.');
  });

  it('says when there is no account', () => {
    const { el } = boot({
      person, dokCases: [], casesLed: [], candidate: null, candidateVisible: false, missions: null, parishAssignments: null,
      formatorFunctions: null, documents: null, supervisions: null, account: null, accountVisible: true
    });
    expect(textOf(el)).toContain('Brak konta.');
    expect(textOf(el)).toContain('Brak spraw.');
  });
});
