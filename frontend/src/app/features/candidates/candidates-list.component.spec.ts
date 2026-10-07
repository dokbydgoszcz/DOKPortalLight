import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CandidatesListComponent } from './candidates-list.component';
import { Candidate } from './candidate.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setSelectByLabel, setup, textOf } from '../../testing/test-helpers';

const lewandowska: Candidate = {
  id: '1', personId: 'p1', personFullName: 'Agnieszka Lewandowska', parishName: null, year: 3,
  attendancePercentage: 94, opinionsCollected: 2, opinionsRequired: 2, status: 'InFormation', isFormationStopped: false, formationStopNote: null, isFormationCompleted: false, yearSinceUtc: '2026-10-07T10:00:00Z', events: [],
  retreats: [{ year: 1, isCompleted: true }, { year: 2, isCompleted: true }, { year: 3, isCompleted: false }]
};
const nowak: Candidate = {
  id: '2', personId: 'p2', personFullName: 'Karolina Nowak', parishName: null, year: 1,
  attendancePercentage: 81, opinionsCollected: 0, opinionsRequired: 2, status: 'InFormation', isFormationStopped: false, formationStopNote: null, isFormationCompleted: false, yearSinceUtc: '2026-10-07T10:00:00Z', events: [], retreats: []
};
const finished: Candidate = {
  id: '3', personId: 'p3', personFullName: 'Ewa Absolwentka', parishName: null, year: 3,
  attendancePercentage: 99, opinionsCollected: 2, opinionsRequired: 2, status: 'Completed', isFormationStopped: false, formationStopNote: null, isFormationCompleted: true, yearSinceUtc: '2026-09-01T10:00:00Z', events: [], retreats: []
};
const stoppedCandidate: Candidate = {
  id: '4', personId: 'p4', personFullName: 'Adam Wycofany', parishName: null, year: 1,
  attendancePercentage: null, opinionsCollected: 0, opinionsRequired: 2, status: 'Stopped', isFormationStopped: true,
  formationStopNote: 'Zrezygnował po rozmowie', isFormationCompleted: false, yearSinceUtc: '2026-10-07T10:00:00Z', events: [], retreats: []
};
const people = [
  { id: 'p1', firstName: 'Jan', lastName: 'Kowalski', fullName: 'Jan Kowalski', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];
const url = api('/api/candidates');

function boot(items: Candidate[] = [lewandowska, nowak], totalCount = items.length) {
  const ctx = setup(CandidatesListComponent);
  ctx.fixture.detectChanges();
  const lists = ctx.http.match(r => r.url === url);
  expect(lists.length).toBe(2);
  lists.forEach(r => r.flush({ items, totalCount, page: 1, pageSize: 20 }));
  ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('CandidatesListComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('shows the per-year candidate count computed from the fetched list', () => {
    const { fixture, el } = boot();

    expect(textOf(el)).toContain('Agnieszka Lewandowska');
    expect(textOf(el)).toContain('Karolina Nowak');
    const component = fixture.componentInstance;
    expect([component.yearOneCount(), component.yearTwoCount(), component.yearThreeCount()]).toEqual([1, 0, 1]);
    expect(component.missingOpinionsCount()).toBe(1);
  });

  it('shows a toast when the list cannot be loaded', () => {
    const { fixture, http } = setup(CandidatesListComponent);
    fixture.detectChanges();

    http.match(r => r.url === url)[0].flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy kandydatów.');
  });

  it('loads the next page with the pagination', () => {
    const ctx = boot([lewandowska], 45);

    clickByText(ctx.el, 'Następna');

    ctx.http.expectOne(r => r.url === url && r.params.get('page') === '2' && r.params.get('pageSize') === '20')
      .flush({ items: [nowak], totalCount: 45, page: 2, pageSize: 20 });
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Karolina Nowak');
  });

  describe('adding a candidate', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Nowy kandydat');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }
    const saveButton = (el: HTMLElement) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

    it('requires a person and posts the typed values with the chosen year and retreat status', async () => {
      const ctx = await openForm();
      expect(saveButton(ctx.el).disabled).toBe(true);

      setSelect(ctx.el, 'select[name="personId"]', 'p1');
      setSelectByLabel(ctx.el, 'select[name="year"]', 'II ROK');
      setInput(ctx.el, 'input[name="attendancePercentage"]', '85');
      setInput(ctx.el, 'input[name="opinionsCollected"]', '1');
      setSelectByLabel(ctx.el, 'select[name="retreat2"]', 'zaliczone');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(false);

      saveButton(ctx.el).click();
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ personId: 'p1', year: 2, attendancePercentage: 85, opinionsCollected: 1, retreats: [{ year: 2, isCompleted: true }] });
      req.flush(nowak);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano kandydata.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.match(r => r.url === url && r.method === 'GET').forEach(r => r.flush({ items: [lewandowska], totalCount: 1, page: 1, pageSize: 20 }));
    });

    it('shows a toast when saving fails and closes the form on cancel', async () => {
      const ctx = await openForm();
      setSelect(ctx.el, 'select[name="personId"]', 'p1');
      ctx.fixture.detectChanges();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać kandydata.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('deleting a candidate', () => {
    it('deletes after confirmation and reloads the list and the statistics', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      expect(confirmSpy).toHaveBeenCalledWith('Usunąć kandydata „Agnieszka Lewandowska”?');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/1`).flush(null);

      expect(toastMessages()).toContain('Kandydat usunięty.');
      expect(ctx.http.match(r => r.method === 'GET' && r.url === url).length).toBe(2);
    });

    it('does nothing when the confirmation is cancelled', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');

      ctx.http.expectNone(r => r.method === 'DELETE');
    });

    it('shows a toast when deleting fails', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się usunąć kandydata.');
    });
  });

  describe('retreats column', () => {
    it('shows the status of each year that has a retreat, and a dash when there is none', () => {
      const { el } = boot();
      const rows = Array.from(el.querySelectorAll('tbody tr')) as HTMLElement[];
      const cell = (row: HTMLElement) => textOf(row.querySelectorAll('td')[6] as HTMLElement);

      expect(cell(rows[0])).toBe('I ✓ II ✓ III oczekuje');
      expect(cell(rows[1])).toBe('—');
    });
  });

  describe('editing a candidate', () => {
    const saveButton = (el: HTMLElement) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

    async function openEdit(rowText = 'Agnieszka Lewandowska') {
      const ctx = boot();
      const row = Array.from(ctx.el.querySelectorAll('tbody tr')).find(r => r.textContent!.includes(rowText)) as HTMLElement;
      (Array.from(row.querySelectorAll<HTMLElement>('.link')).find(l => l.textContent!.trim() === 'Edytuj'))!.click();
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('opens the form filled with the candidate', async () => {
      const ctx = await openEdit();

      expect(textOf(ctx.el)).toContain('Edytuj kandydata');
      expect((ctx.el.querySelector('select[name="personId"]') as HTMLSelectElement).value).toBe('p1');
      expect((ctx.el.querySelector('select[name="year"]') as HTMLSelectElement).value).toContain('3');
      expect((ctx.el.querySelector('input[name="attendancePercentage"]') as HTMLInputElement).value).toBe('94');
      expect((ctx.el.querySelector('select[name="retreat3"]') as HTMLSelectElement).value).toBe('pending');
    });

    it('saves the change with PUT including the retreats, confirms and reloads', async () => {
      const ctx = await openEdit();

      setSelectByLabel(ctx.el, 'select[name="retreat3"]', 'zaliczone');
      setInput(ctx.el, 'input[name="opinionsCollected"]', '1');
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/1`);
      expect(req.request.body).toEqual({
        personId: 'p1', year: 3, attendancePercentage: 94, opinionsCollected: 1, isFormationStopped: false, isFormationCompleted: false,
        retreats: [{ year: 1, isCompleted: true }, { year: 2, isCompleted: true }, { year: 3, isCompleted: true }]
      });
      req.flush(lewandowska);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Zapisano zmiany.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.match(r => r.method === 'GET' && r.url === url).forEach(r => r.flush({ items: [lewandowska], totalCount: 1, page: 1, pageSize: 20 }));
    });

    it('shows the server message, or a generic one, when saving fails and keeps the form open', async () => {
      const ctx = await openEdit();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'PUT').flush({ title: 'Rok rekolekcji musi być z zakresu 1–3.' }, { status: 400, statusText: 'Bad Request' });
      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Rok rekolekcji musi być z zakresu 1–3.');
      expect(toastMessages()).toContain('Nie udało się zapisać zmian.');
      expect(ctx.el.querySelector('.modal')).not.toBeNull();
    });

    it('goes back to adding after an edit was cancelled', async () => {
      const ctx = await openEdit();
      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Nowy kandydat');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      expect(textOf(ctx.el)).toContain('Nowy kandydat SKŚP');
      expect((ctx.el.querySelector('select[name="retreat1"]') as HTMLSelectElement).value).toBe('');
    });

    it('hides editing from users who cannot manage candidates', () => {
      const ctx = setup(CandidatesListComponent, { granted: ['Candidates.View'] });
      ctx.fixture.detectChanges();
      ctx.http.match(r => r.url === url).forEach(r => r.flush({ items: [lewandowska], totalCount: 1, page: 1, pageSize: 20 }));
      ctx.http.match(r => r.url === api('/api/people')).forEach(r => r.flush(paged(people)));
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).not.toContain('Edytuj');
    });
  });

  describe('formation status', () => {
    const rowOf = (el: HTMLElement, name: string) =>
      Array.from(el.querySelectorAll('tbody tr')).find(r => r.textContent!.includes(name)) as HTMLElement;

    it('counts only those still in formation in the per-year cards, and shows how many finished', () => {
      const { fixture } = boot([lewandowska, nowak, finished, stoppedCandidate]);
      const component = fixture.componentInstance;

      expect([component.yearOneCount(), component.yearTwoCount(), component.yearThreeCount()]).toEqual([1, 0, 1]);
      expect(component.missingOpinionsCount()).toBe(1);
      expect(component.completedCount()).toBe(1);
    });

    it('labels each candidate: the year, finished formation, or stopped with the reason', () => {
      const { el } = boot([lewandowska, finished, stoppedCandidate]);

      expect(textOf(rowOf(el, 'Agnieszka'))).toContain('3 ROK');
      expect(textOf(rowOf(el, 'Ewa Absolwentka'))).toContain('Ukończył formację');
      expect(textOf(rowOf(el, 'Ewa Absolwentka'))).not.toContain('3 ROK');
      expect(textOf(rowOf(el, 'Adam Wycofany'))).toContain('Formacja zatrzymana');
      expect(textOf(rowOf(el, 'Adam Wycofany'))).toContain('Zrezygnował po rozmowie');
    });
  });

  describe('stopping the formation', () => {
    const saveButton = (el: HTMLElement) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

    async function openEdit(candidate: Candidate = lewandowska) {
      const ctx = boot([candidate]);
      (Array.from(ctx.el.querySelectorAll<HTMLElement>('tbody .link')).find(l => l.textContent!.trim() === 'Edytuj'))!.click();
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('shows the reason field only after ticking the checkbox, and needs the reason to save', async () => {
      const ctx = await openEdit();
      const checkbox = () => ctx.el.querySelector('input[name="isFormationStopped"]') as HTMLInputElement;

      expect(ctx.el.querySelector('textarea[name="formationStopNote"]')).toBeNull();
      expect(saveButton(ctx.el).disabled).toBe(false);

      checkbox().click();
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('textarea[name="formationStopNote"]')).not.toBeNull();
      expect(saveButton(ctx.el).disabled).toBe(true);
      expect(textOf(ctx.el)).toContain('Uzupełnij pola oznaczone *');

      setInput(ctx.el, 'textarea[name="formationStopNote"]', '   ');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(true);

      setInput(ctx.el, 'textarea[name="formationStopNote"]', 'Zrezygnowała');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(false);
    });

    it('sends the stop flag with the reason', async () => {
      const ctx = await openEdit();
      (ctx.el.querySelector('input[name="isFormationStopped"]') as HTMLInputElement).click();
      ctx.fixture.detectChanges();
      setInput(ctx.el, 'textarea[name="formationStopNote"]', 'Zrezygnowała');
      ctx.fixture.detectChanges();

      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/1`);
      expect(req.request.body.isFormationStopped).toBe(true);
      expect(req.request.body.formationStopNote).toBe('Zrezygnowała');
      req.flush(lewandowska);
      ctx.http.match(r => r.method === 'GET' && r.url === url).forEach(r => r.flush({ items: [lewandowska], totalCount: 1, page: 1, pageSize: 20 }));
    });

    it('shows the saved stop in the edit form, and unticking it drops the reason', async () => {
      const ctx = await openEdit(stoppedCandidate);
      const checkbox = () => ctx.el.querySelector('input[name="isFormationStopped"]') as HTMLInputElement;

      expect(checkbox().checked).toBe(true);
      expect((ctx.el.querySelector('textarea[name="formationStopNote"]') as HTMLTextAreaElement).value).toBe('Zrezygnował po rozmowie');

      checkbox().click();
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT');
      expect(req.request.body.isFormationStopped).toBe(false);
      req.flush(nowak);
      ctx.http.match(r => r.method === 'GET' && r.url === url).forEach(r => r.flush({ items: [nowak], totalCount: 1, page: 1, pageSize: 20 }));
    });
  });

  describe('moving candidates to the next year', () => {
    const advanceUrl = `${url}/advance`;
    const rowOf = (el: HTMLElement, name: string) =>
      Array.from(el.querySelectorAll('tbody tr')).find(r => r.textContent!.includes(name)) as HTMLElement;
    const checkbox = (el: HTMLElement, name: string) => rowOf(el, name).querySelector('input.select-candidate') as HTMLInputElement;
    const bulkButton = (el: HTMLElement) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.includes('Przenieś zaznaczonych'))!;

    it('shows since when each candidate is in the current year', () => {
      const { el } = boot();

      expect(textOf(rowOf(el, 'Agnieszka'))).toContain('od 07.10.2026');
    });

    it('offers the move that fits the year: to the next year, or finishing the formation after the third', () => {
      const { el } = boot([nowak, lewandowska]);

      expect(textOf(rowOf(el, 'Karolina'))).toContain('Przenieś do II roku');
      expect(textOf(rowOf(el, 'Agnieszka'))).toContain('Ukończył formację');
    });

    it('moves one candidate after confirmation, says what happened and reloads', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot([nowak]);

      clickByText(rowOf(ctx.el, 'Karolina'), 'Przenieś do II roku', '.link');
      expect(confirmSpy).toHaveBeenCalledWith('Przenieść „Karolina Nowak” do II roku?');
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === advanceUrl);
      expect(req.request.body).toEqual({ candidateIds: ['2'] });
      req.flush({ advanced: 1, completed: 0, skipped: [] });

      expect(toastMessages()).toContain('Przeniesiono do następnego roku: 1.');
      expect(ctx.http.match(r => r.method === 'GET' && r.url === url).length).toBe(2);
    });

    it('asks differently for finishing the formation, and does nothing when declined', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = boot([lewandowska]);

      clickByText(rowOf(ctx.el, 'Agnieszka'), 'Ukończył formację', '.link');

      expect(confirmSpy).toHaveBeenCalledWith('Zakończyć formację kandydata „Agnieszka Lewandowska”?');
      ctx.http.expectNone(r => r.method === 'POST');
    });

    it('keeps the bulk button disabled until someone is selected, and counts the selection', () => {
      const ctx = boot([lewandowska, nowak]);
      expect(bulkButton(ctx.el).disabled).toBe(true);

      checkbox(ctx.el, 'Karolina').click();
      ctx.fixture.detectChanges();
      expect(bulkButton(ctx.el).disabled).toBe(false);
      expect(bulkButton(ctx.el).textContent).toContain('(1)');

      checkbox(ctx.el, 'Karolina').click();
      ctx.fixture.detectChanges();
      expect(bulkButton(ctx.el).disabled).toBe(true);
    });

    it('moves all the selected at once and clears the selection', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot([lewandowska, nowak]);
      checkbox(ctx.el, 'Karolina').click();
      checkbox(ctx.el, 'Agnieszka').click();
      ctx.fixture.detectChanges();

      bulkButton(ctx.el).click();
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === advanceUrl);
      expect(req.request.body.candidateIds.sort()).toEqual(['1', '2']);
      req.flush({ advanced: 1, completed: 1, skipped: [] });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Przeniesiono do następnego roku: 1.');
      expect(toastMessages()).toContain('Ukończyło formację: 1.');
      expect(bulkButton(ctx.el).disabled).toBe(true);
      expect(checkbox(ctx.el, 'Karolina').checked).toBe(false);
    });

    it('selects all those who can be moved with the header checkbox, never finished or stopped ones', () => {
      const ctx = boot([nowak, lewandowska, finished, stoppedCandidate]);

      (ctx.el.querySelector('input.select-all') as HTMLInputElement).click();
      ctx.fixture.detectChanges();

      expect(checkbox(ctx.el, 'Karolina').checked).toBe(true);
      expect(checkbox(ctx.el, 'Agnieszka').checked).toBe(true);
      expect(checkbox(ctx.el, 'Ewa Absolwentka').disabled).toBe(true);
      expect(checkbox(ctx.el, 'Adam Wycofany').disabled).toBe(true);
      expect(bulkButton(ctx.el).textContent).toContain('(2)');

      (ctx.el.querySelector('input.select-all') as HTMLInputElement).click();
      ctx.fixture.detectChanges();
      expect(bulkButton(ctx.el).textContent).toContain('(0)');
    });

    it('lists the skipped candidates with the reason', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot([nowak]);
      clickByText(rowOf(ctx.el, 'Karolina'), 'Przenieś do II roku', '.link');

      ctx.http.expectOne(r => r.url === advanceUrl).flush({
        advanced: 0, completed: 0, skipped: [{ candidateId: '2', personFullName: 'Karolina Nowak', reason: 'Formacja zatrzymana.' }]
      });

      expect(toastMessages()).toContain('Karolina Nowak: Formacja zatrzymana.');
    });

    it('shows the server message, or a generic one, when the move fails', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot([nowak]);

      clickByText(rowOf(ctx.el, 'Karolina'), 'Przenieś do II roku', '.link');
      ctx.http.expectOne(r => r.url === advanceUrl).flush({ title: 'Zaznacz co najmniej jednego kandydata.' }, { status: 400, statusText: 'Bad Request' });
      clickByText(rowOf(ctx.el, 'Karolina'), 'Przenieś do II roku', '.link');
      ctx.http.expectOne(r => r.url === advanceUrl).flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Zaznacz co najmniej jednego kandydata.');
      expect(toastMessages()).toContain('Nie udało się przenieść kandydatów.');
    });

    it('is hidden from users who cannot manage candidates', () => {
      const ctx = setup(CandidatesListComponent, { granted: ['Candidates.View'] });
      ctx.fixture.detectChanges();
      ctx.http.match(r => r.url === url).forEach(r => r.flush({ items: [nowak], totalCount: 1, page: 1, pageSize: 20 }));
      ctx.http.match(r => r.url === api('/api/people')).forEach(r => r.flush(paged(people)));
      ctx.fixture.detectChanges();

      expect(ctx.el.querySelector('input.select-candidate')).toBeNull();
      expect(ctx.el.querySelector('input.select-all')).toBeNull();
      expect(textOf(ctx.el)).not.toContain('Przenieś');
    });
  });
});
