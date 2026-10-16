import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { ToastService } from '../../core/notifications/toast.service';
import { PersonCardComponent } from './person-card.component';
import { api, clickByText, setup, textOf } from '../../testing/test-helpers';

const person = {
  id: 'p1', firstName: 'Ewa', lastName: 'Maj', fullName: 'Ewa Maj', email: 'ewa@example.org', phone: null, birthDate: null,
  parishId: null, parishName: 'Fara', notes: null, nameDayMonth: null, nameDayDay: null,
  functions: [{ id: 'f1', type: 'Catechist', parishId: null, parishName: null, institutedOn: null, notes: null }]
};

function boot(body: object | null, status = 200, query: Record<string, string> = {}) {
  const ctx = setup(PersonCardComponent, {
    providers: [{ provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'p1' }), queryParamMap: convertToParamMap(query) } } }]
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
    expect(text).toContain('grupa superwizyjna: Grupa A');
    expect(text).toContain('Wniosek o misję kanoniczną');
    expect(text).toContain('Obecny(a) na 3 z 4');
    expect(text).toContain('Katechista prowadzący');
    expect(text).toContain('Powiązań w systemie: 5');
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
    expect(text).not.toContain('Szkoła katechistów');
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
    expect(textOf(el)).toContain('Nie jest podopiecznym DOK.');
  });
  it('shows the initials without a photo, uploads one and removes it', () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:foto');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const ctx = boot({
      person, dokCases: null, casesLed: null, candidate: null, candidateVisible: false, missions: null, parishAssignments: null,
      formatorFunctions: null, documents: null, supervisions: null, account: null, accountVisible: false
    });
    expect(ctx.el.querySelector('.initials')?.textContent?.trim()).toBe('EM');

    const input = ctx.el.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File([new Uint8Array([0x89, 0x50])], 'ja.png', { type: 'image/png' });
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === api('/api/people/p1/photo'));
    expect(req.request.body instanceof FormData).toBe(true);
    req.flush({ ...person, photoUpdatedAtUtc: '2026-10-14T08:00:00Z' });
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('.portrait img')?.getAttribute('src')).toBe('blob:foto');

    vi.spyOn(window, 'confirm').mockReturnValue(true);
    clickByText(ctx.el, 'Usuń', '.photo-actions span');
    ctx.http.expectOne(r => r.method === 'DELETE' && r.url === api('/api/people/p1/photo')).flush(null);
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('.portrait img')).toBeNull();
    expect(TestBed.inject(ToastService).toasts().map(t => t.message)).toContain('Usunięto zdjęcie.');
  });

  it('loads an existing photo, and refuses wrong files before sending', () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:foto');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const ctx = boot({
      person: { ...person, photoUpdatedAtUtc: '2026-10-01T00:00:00Z' }, dokCases: null, casesLed: null, candidate: null, candidateVisible: false,
      missions: null, parishAssignments: null, formatorFunctions: null, documents: null, supervisions: null, account: null, accountVisible: false
    });
    ctx.http.expectOne(api('/api/people/p1/photo')).flush(new Blob(['x']));
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('.portrait img')).not.toBeNull();

    const component = ctx.fixture.componentInstance;
    const pick = (file: File) => component.onPhotoSelected({ target: { files: [file], value: '' } } as unknown as Event);
    pick(new File(['%PDF'], 'skan.pdf', { type: 'application/pdf' }));
    pick(new File([new Uint8Array(5 * 1024 * 1024 + 1)], 'duze.jpg', { type: 'image/jpeg' }));
    ctx.http.expectNone(r => r.method === 'PUT');
    const messages = TestBed.inject(ToastService).toasts().map(t => t.message);
    expect(messages).toContain('Zdjęcie musi być w formacie JPG, PNG lub WEBP.');
    expect(messages).toContain('Zdjęcie jest za duże – maksymalnie 5 MB.');

    pick(new File(['x'], 'ok.jpg', { type: 'image/jpeg' }));
    ctx.http.expectOne(r => r.method === 'PUT').flush({ title: 'Zdjęcie musi być w formacie JPG, PNG lub WEBP.' }, { status: 400, statusText: 'Bad Request' });
    expect(component.uploading()).toBe(false);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    component.removePhoto();
    ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Error' });
    expect(TestBed.inject(ToastService).toasts().map(t => t.message)).toContain('Nie udało się usunąć zdjęcia.');
    ctx.fixture.destroy();
  });
  it('goes back to the page it was opened from', () => {
    const fromUsers = boot(null, 404, { z: 'uzytkownicy' });
    const back = fromUsers.el.querySelector('a.back')!;
    expect(back.textContent).toContain('Wróć do użytkowników i ról');
    expect(back.getAttribute('href')).toBe('/admin/users');
  });

  it('goes back to the list of people by default', () => {
    const { el } = boot(null, 404, { z: 'nieznane' });
    expect(el.querySelector('a.back')!.getAttribute('href')).toBe('/people');
  });

  describe('personal data (RODO)', () => {
    const empty = {
      dokCases: null, casesLed: null, candidate: null, candidateVisible: false, missions: null, parishAssignments: null,
      formatorFunctions: null, documents: null, supervisions: null, account: null, accountVisible: false
    };
    const toasts = () => TestBed.inject(ToastService).toasts().map(t => t.message);

    it('downloads a copy of the data, and reports a failure', () => {
      const create = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:dane');
      vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
      vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
      const ctx = boot({ person, ...empty });

      clickByText(ctx.el, 'Pobierz kopię danych');
      ctx.http.expectOne(api('/api/people/p1/rodo/export')).flush(new Blob(['{}']));
      expect(create).toHaveBeenCalled();

      clickByText(ctx.el, 'Pobierz kopię danych');
      ctx.http.expectOne(api('/api/people/p1/rodo/export')).flush(new Blob(['x']), { status: 500, statusText: 'Error' });
      expect(toasts()).toContain('Nie udało się przygotować kopii danych.');
      vi.restoreAllMocks();
    });

    it('removes the data only after the surname is typed, then shows the anonymized card', () => {
      const ctx = boot({ person, ...empty });
      const ask = vi.spyOn(window, 'prompt');

      ask.mockReturnValueOnce(null);
      clickByText(ctx.el, 'Usuń dane osoby');
      ask.mockReturnValueOnce('Kowalska');
      clickByText(ctx.el, 'Usuń dane osoby');
      expect(toasts()).toContain('Nazwisko się nie zgadza – dane nie zostały usunięte.');
      ctx.http.expectNone(api('/api/people/p1/rodo/anonymize'));

      ask.mockReturnValueOnce(' maj ');
      clickByText(ctx.el, 'Usuń dane osoby');
      ctx.http.expectOne(r => r.method === 'POST' && r.url === api('/api/people/p1/rodo/anonymize')).flush({ title: 'Ta osoba prowadzi 2 otwartych spraw DOK.' }, { status: 400, statusText: 'Bad Request' });
      expect(toasts()).toContain('Ta osoba prowadzi 2 otwartych spraw DOK.');

      ask.mockReturnValueOnce('Maj');
      clickByText(ctx.el, 'Usuń dane osoby');
      ctx.http.expectOne(r => r.method === 'POST' && r.url === api('/api/people/p1/rodo/anonymize')).flush(null);
      expect(toasts()).toContain('Dane osoby zostały usunięte (zanonimizowane).');
      ctx.http.expectOne(api('/api/people/p1/overview')).flush({
        person: { ...person, firstName: 'Osoba', lastName: 'zanonimizowana 123456', fullName: 'Osoba zanonimizowana 123456', email: null, anonymizedAtUtc: '2026-10-14T10:00:00Z' },
        ...empty
      });
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).toContain('Dane tej osoby usunięto (anonimizacja) 14.10.2026');
      expect(ctx.el.textContent).not.toContain('Pobierz kopię danych');
      vi.restoreAllMocks();
    });

    it('is not offered without the right to manage accounts', () => {
      const ctx = setup(PersonCardComponent, {
        granted: ['People.View'],
        providers: [{ provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'p1' }), queryParamMap: convertToParamMap({}) } } }]
      });
      ctx.fixture.detectChanges();
      ctx.http.expectOne(api('/api/people/p1/overview')).flush({ person, ...empty });
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).not.toContain('RODO');
    });
  });

  it('shows the home address when there is one', () => {
    const empty = { dokCases: null, casesLed: null, candidate: null, candidateVisible: false, missions: null, parishAssignments: null,
      formatorFunctions: null, documents: null, supervisions: null, account: null, accountVisible: false };
    const ctx = boot({ person: { ...person, addressStreet: 'ul. Długa 5', addressPostalCode: '85-001', addressCity: 'Bydgoszcz' }, ...empty });
    expect(textOf(ctx.el)).toContain('ul. Długa 5, 85-001 Bydgoszcz');
    expect(ctx.fixture.componentInstance.address({ addressCity: 'Nakło' })).toBe('Nakło');
    expect(ctx.fixture.componentInstance.address({})).toBeNull();
  });
});
