import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ParishesListComponent } from './parishes-list.component';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setup, textOf } from '../../testing/test-helpers';

const parishes = [
  { id: 'pa1', name: 'św. Mateusza', city: 'Bydgoszcz' },
  { id: 'pa2', name: 'Matki Bożej', city: 'Toruń' }
];
const url = api('/api/parishes');

function boot(items = parishes) {
  const ctx = setup(ParishesListComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(items);
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('ParishesListComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders the parishes and an empty-state text', () => {
    const ctx = boot();
    expect(textOf(ctx.el)).toContain('św. Mateusza');
    expect(textOf(ctx.el)).toContain('Toruń');

    TestBed.resetTestingModule();
    expect(textOf(boot([]).el)).toContain('Brak parafii.');
  });

  it('filters the list by name or city while typing', () => {
    const ctx = boot();

    setInput(ctx.el, 'input[placeholder="Szukaj…"]', 'toruń');
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Matki Bożej');
    expect(textOf(ctx.el)).not.toContain('św. Mateusza');

    setInput(ctx.el, 'input[placeholder="Szukaj…"]', 'mateusza');
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('św. Mateusza');
    expect(textOf(ctx.el)).not.toContain('Matki Bożej');
  });

  it('shows a toast when the list cannot be loaded', () => {
    const { fixture, http } = setup(ParishesListComponent);
    fixture.detectChanges();

    http.expectOne(url).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy parafii.');
  });

  describe('adding a parish', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj parafię');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('refuses an empty name without calling the API', async () => {
      const ctx = await openForm();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      expect(toastMessages()).toContain('Podaj nazwę parafii.');
      ctx.http.expectNone(r => r.method === 'POST');
    });

    it('posts the typed parish, confirms and reloads', async () => {
      const ctx = await openForm();

      setInput(ctx.el, 'input[name="name"]', 'św. Antoniego');
      setInput(ctx.el, 'input[name="city"]', 'Bydgoszcz');
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ name: 'św. Antoniego', city: 'Bydgoszcz', pastorPersonId: null });
      req.flush(parishes[0]);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano parafię.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush(parishes);
    });

    it('shows a toast when adding fails and closes the form on cancel', async () => {
      const ctx = await openForm();
      setInput(ctx.el, 'input[name="name"]', 'św. Antoniego');

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać parafii.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('deleting a parish', () => {
    it('names the parish and city in the confirmation, deletes and reloads', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      expect(confirmSpy).toHaveBeenCalledWith('Usunąć parafię „św. Mateusza” (Bydgoszcz)?');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/pa1`).flush(null);

      expect(toastMessages()).toContain('Parafia usunięta.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([parishes[1]]);
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

      expect(toastMessages()).toContain('Nie udało się usunąć parafii.');
    });

    it('shows why the parish cannot be deleted', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      ctx.http.expectOne(r => r.method === 'DELETE')
        .flush({ title: 'Nie można usunąć parafii, bo ma: proboszcz (1).' }, { status: 400, statusText: 'Bad Request' });

      expect(toastMessages()).toContain('Nie można usunąć parafii, bo ma: proboszcz (1).');
    });
  });

  describe('editing a parish', () => {
    const saveButton = (el: HTMLElement) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;
    const editLink = (el: HTMLElement, rowText: string) =>
      Array.from(Array.from(el.querySelectorAll('tbody tr')).find(r => r.textContent!.includes(rowText))!.querySelectorAll<HTMLElement>('.link'))
        .find(l => l.textContent!.trim() === 'Edytuj')!;

    async function openEdit(rowText = 'Matki Bożej') {
      const ctx = boot();
      editLink(ctx.el, rowText).click();
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('opens the form filled with the chosen parish', async () => {
      const ctx = await openEdit();

      expect(textOf(ctx.el)).toContain('Edytuj parafię');
      expect((ctx.el.querySelector('input[name="name"]') as HTMLInputElement).value).toBe('Matki Bożej');
      expect((ctx.el.querySelector('input[name="city"]') as HTMLInputElement).value).toBe('Toruń');
    });

    it('saves the change with PUT, confirms and reloads', async () => {
      const ctx = await openEdit();

      setInput(ctx.el, 'input[name="name"]', 'Matki Bożej Częstochowskiej');
      setInput(ctx.el, 'input[name="city"]', 'Chełmża');
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/pa2`);
      expect(req.request.body).toEqual({ name: 'Matki Bożej Częstochowskiej', city: 'Chełmża', pastorPersonId: null });
      req.flush({ id: 'pa2', name: 'Matki Bożej Częstochowskiej', city: 'Chełmża' });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Zapisano zmiany.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush(parishes);
    });

    it('refuses an emptied name without calling the API', async () => {
      const ctx = await openEdit();

      setInput(ctx.el, 'input[name="name"]', '   ');
      saveButton(ctx.el).click();

      expect(toastMessages()).toContain('Podaj nazwę parafii.');
      ctx.http.expectNone(r => r.method === 'PUT');
    });

    it('shows a toast when saving fails and keeps the form open', async () => {
      const ctx = await openEdit();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Nie udało się zapisać zmian.');
      expect(ctx.el.querySelector('.modal')).not.toBeNull();
    });

    it('goes back to adding after an edit was cancelled', async () => {
      const ctx = await openEdit();
      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Dodaj parafię');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      expect(textOf(ctx.el)).toContain('Nowa parafia');
      expect((ctx.el.querySelector('input[name="name"]') as HTMLInputElement).value).toBe('');
    });

    it('hides add, edit and delete from users who cannot manage parishes', () => {
      const ctx = setup(ParishesListComponent, { granted: [] });
      ctx.fixture.detectChanges();
      ctx.http.expectOne(url).flush(parishes);
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).toContain('Matki Bożej');
      expect(textOf(ctx.el)).not.toContain('Dodaj parafię');
      expect(textOf(ctx.el)).not.toContain('Edytuj');
      expect(textOf(ctx.el)).not.toContain('Usuń');
    });
  });
  describe('pastor', () => {
    it('shows the pastor with a link to the person card and lets the pastor be chosen', async () => {
      const ctx = setup(ParishesListComponent);
      ctx.fixture.detectChanges();
      ctx.http.expectOne(url).flush([{ id: 'pa1', name: 'św. Wojciecha', city: 'Bydgoszcz', pastorPersonId: 'p1', pastorFullName: 'ks. Jan Nowak' }]);
      ctx.fixture.detectChanges();
      const link = ctx.el.querySelector('a.person-link')!;
      expect(link.textContent).toContain('ks. Jan Nowak');
      expect(link.getAttribute('href')).toBe('/people/p1?z=parafie');

      clickByText(ctx.el, 'Edytuj');
      ctx.http.expectOne(r => r.url === api('/api/people') && r.params.get('function') === 'Pastor').flush(paged([
        { id: 'p1', fullName: 'ks. Jan Nowak', functions: [{ type: 'Pastor', parishName: 'św. Wojciecha' }] },
        { id: 'p2', fullName: 'ks. Adam Lis', functions: [{ type: 'Pastor', parishName: 'Fara' }] }
      ]));
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      expect(textOf(ctx.el)).toContain('ks. Adam Lis (obecnie: Fara)');
      const select = ctx.el.querySelector('select[name="pastorPersonId"]') as HTMLSelectElement;
      select.selectedIndex = 2;
      select.dispatchEvent(new Event('change'));
      ctx.fixture.detectChanges();
      clickByText(ctx.el, "Zapisz", ".modal-foot button");

      const req = ctx.http.expectOne(r => r.method === 'PUT');
      expect(req.request.body.pastorPersonId).toBe('p2');
      req.flush({ title: 'Nie znaleziono osoby wskazanej jako proboszcz.' }, { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie znaleziono osoby wskazanej jako proboszcz.');
    });

    it('keeps the form usable when the pastor list cannot be loaded', () => {
      const ctx = setup(ParishesListComponent);
      ctx.fixture.detectChanges();
      ctx.http.expectOne(url).flush([]);
      ctx.fixture.componentInstance.openAddForm();
      ctx.http.expectOne(r => r.url === api('/api/people')).flush('x', { status: 403, statusText: 'Forbidden' });
      expect(ctx.fixture.componentInstance.pastors()).toEqual([]);
    });
  });

  describe('importing pastors', () => {
    const preview = {
      applied: false, newParishes: 1, matchedParishes: 1, newPeople: 1, existingPeople: 1, pastorsSet: 1, pastorsUnchanged: 0, pastorsReplaced: 1,
      rows: [
        { row: 2, deanery: 'Bydgoszcz V – Fordon', city: 'Bydgoszcz', parishName: 'św. Mateusza', newParish: false, pastorFullName: 'Jan Nowak', newPerson: false, pastorChange: 'replaced', previousPastor: 'Stary Proboszcz', warnings: ['W bazie jest 2 osób o tym imieniu i nazwisku – wybrano jedną, sprawdź.'] },
        { row: 3, deanery: null, city: 'Bądecz', parishName: 'św. Józefa', newParish: true, pastorFullName: 'Piotr Kot', newPerson: true, pastorChange: 'set', previousPastor: null, warnings: [] }
      ]
    };

    function pick(ctx: ReturnType<typeof boot>) {
      const input = ctx.el.querySelector('.import-modal input[type="file"]') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: [new File(['x'], 'proboszczowie.xlsx')], configurable: true });
      input.dispatchEvent(new Event('change'));
    }

    it('shows a preview first and imports only after confirmation', () => {
      const ctx = boot();
      clickByText(ctx.el, 'Importuj proboszczów');
      ctx.fixture.detectChanges();
      const importButton = () => Array.from(ctx.el.querySelectorAll('.modal-foot button')).find(b => b.textContent!.trim() === 'Importuj') as HTMLButtonElement;
      expect(importButton().disabled).toBe(true);

      pick(ctx);
      const previewReq = ctx.http.expectOne(`${url}/import/preview`);
      expect(previewReq.request.body instanceof FormData).toBe(true);
      previewReq.flush(preview);
      ctx.fixture.detectChanges();
      const text = textOf(ctx.el);
      expect(text).toContain('nowe parafie: 1');
      expect(text).toContain('było: Stary Proboszcz');
      expect(text).toContain('⚠ W bazie jest 2 osób');
      expect(text).toContain('nowa osoba');

      importButton().click();
      ctx.http.expectOne(`${url}/import`).flush({ ...preview, applied: true });
      expect(toastMessages()).toContain('Zaimportowano: 1 nowych parafii, 1 nowych osób, 2 przypisań proboszcza.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush(parishes);
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.import-modal')).toBeNull();
    });

    it('explains a file that cannot be read, and a failed import', () => {
      const ctx = boot();
      clickByText(ctx.el, 'Importuj proboszczów');
      ctx.fixture.detectChanges();
      pick(ctx);
      ctx.http.expectOne(`${url}/import/preview`).flush({ title: 'To nie jest plik Excel (.xlsx).' }, { status: 400, statusText: 'Bad Request' });
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).toContain('To nie jest plik Excel');

      pick(ctx);
      ctx.http.expectOne(`${url}/import/preview`).flush(preview);
      ctx.fixture.detectChanges();
      ctx.fixture.componentInstance.runImport(true);
      ctx.http.expectOne(`${url}/import`).flush('x', { status: 500, statusText: 'Error' });
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).toContain('Import się nie udał.');
      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.import-modal')).toBeNull();
    });
  });

  it('shows the deanery and contact, finds parishes by deanery and keeps them when editing', async () => {
    const ctx = boot([{ id: 'pa3', name: 'św. Józefa', city: 'Bądecz', deanery: 'Wysoka', address: 'Bądecz 3', phone: '67 287 11 09', email: 'p@example.org', website: 'www.p.example.org', pastorPersonId: null } as never]);
    expect(textOf(ctx.el)).toContain('Wysoka');
    expect(ctx.el.querySelector('.contact a[href="https://www.p.example.org"]')).not.toBeNull();
    const search = ctx.el.querySelector('.search input') as HTMLInputElement;
    search.value = 'wysoka';
    search.dispatchEvent(new Event('input'));
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('św. Józefa');

    clickByText(ctx.el, 'Edytuj');
    ctx.fixture.detectChanges();
    ctx.http.match(() => true).forEach(r => r.flush({ items: [], totalCount: 0, page: 1, pageSize: 200 }));
    await ctx.fixture.whenStable();
    clickByText(ctx.el, 'Zapisz', '.modal-foot button');
    const put = ctx.http.expectOne(r => r.method === 'PUT');
    expect(put.request.body).toMatchObject({ deanery: 'Wysoka', address: 'Bądecz 3', phone: '67 287 11 09', email: 'p@example.org', website: 'www.p.example.org' });
  });

  it('saves the parish contact typed in the form', async () => {
    const ctx = boot([{ id: 'pa4', name: 'Fara', city: null, website: 'https://fara.example.org', phone: '52 1', pastorPersonId: null } as never]);
    expect(ctx.el.querySelector('.contact a[href="https://fara.example.org"]')).not.toBeNull();
    clickByText(ctx.el, '＋ Dodaj parafię');
    ctx.fixture.detectChanges();
    ctx.http.match(() => true).forEach(r => r.flush({ items: [], totalCount: 0, page: 1, pageSize: 200 }));
    await ctx.fixture.whenStable();
    setInput(ctx.el, 'input[name="name"]', 'św. Rocha');
    setInput(ctx.el, 'input[name="deanery"]', 'Złotów I');
    setInput(ctx.el, 'input[name="address"]', 'ul. Kościelna 1');
    setInput(ctx.el, 'input[name="phone"]', '67 000');
    setInput(ctx.el, 'input[name="email"]', 'roch@example.org');
    setInput(ctx.el, 'input[name="website"]', 'roch.example.org');
    clickByText(ctx.el, 'Zapisz', '.modal-foot button');
    expect(ctx.http.expectOne(r => r.method === 'POST').request.body).toMatchObject({
      name: 'św. Rocha', deanery: 'Złotów I', address: 'ul. Kościelna 1', phone: '67 000', email: 'roch@example.org', website: 'roch.example.org'
    });
  });

  it('ignores an empty file choice and shows that the file is being checked', () => {
    const ctx = boot();
    clickByText(ctx.el, 'Importuj proboszczów');
    ctx.fixture.detectChanges();
    const component = ctx.fixture.componentInstance;
    component.runImport(false);
    const input = ctx.el.querySelector('.import-modal input[type="file"]') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [], configurable: true });
    input.dispatchEvent(new Event('change'));
    ctx.http.expectNone(`${url}/import/preview`);

    Object.defineProperty(input, 'files', { value: [new File(['x'], 'a.xlsx')], configurable: true });
    input.dispatchEvent(new Event('change'));
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Sprawdzanie');
    component.runImport(false);
    ctx.http.expectOne(`${url}/import/preview`).flush({ applied: false, newParishes: 0, matchedParishes: 0, newPeople: 0, existingPeople: 0, pastorsSet: 0, pastorsUnchanged: 0, pastorsReplaced: 0, rows: [] });
    clickByText(ctx.el, '×', '.import-modal .close');
  });
});
