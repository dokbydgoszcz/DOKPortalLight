import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MissionsListComponent } from './missions-list.component';
import { Mission } from './mission.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const mission: Mission = {
  id: '1', personId: 'p1', personFullName: 'Anna Maj', servicePlace: 'Parafia św. Mateusza',
  missionStartDate: '2023-10-15', missionEndDate: '2026-10-14', grantedDate: null, grantedPlace: null,
  supervisionGroup: 'Grupa A', status: 'wygasa', sentToDok: false
};
const sentMission: Mission = { ...mission, id: '2', personFullName: 'Jan Kowalski', sentToDok: true };
const people = [
  { id: 'p1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];
const url = api('/api/missions');

function boot(items: Mission[] = [mission], totalCount = items.length) {
  const ctx = setup(MissionsListComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === url).flush({ items, totalCount, page: 1, pageSize: 20 });
  ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
  ctx.http.expectOne(r => r.url === api('/api/parishes')).flush([]);
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('MissionsListComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders missions returned from the API with their status', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('Anna Maj');
    expect(textOf(el)).toContain('wygasa');
  });

  it('marks the missions of catechists sent to DOK in their own column', () => {
    const { el } = boot([mission, sentMission]);

    const rows = Array.from(el.querySelectorAll('tbody tr'));
    expect(el.querySelector('thead')!.textContent).toContain('Posłany do DOK');
    expect(rows[0].querySelector('.sent-to-dok')!.textContent!.trim()).toBe('—');
    expect(rows[1].querySelector('.sent-to-dok')!.textContent!.trim()).toBe('tak');
  });

  it('colours expired and expiring missions red and active ones green', () => {
    const { fixture } = boot();
    const component = fixture.componentInstance;

    expect(component.statusPillClass('wygasła')).toBe('pill red');
    expect(component.statusPillClass('wygasa')).toBe('pill red');
    expect(component.statusPillClass('aktywna')).toBe('pill green');
  });

  it('shows a toast when the list cannot be loaded', () => {
    const { fixture, http } = setup(MissionsListComponent);
    fixture.detectChanges();

    http.expectOne(r => r.url === url).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy misji.');
  });

  it('loads the next page with the pagination', () => {
    const ctx = boot([mission], 45);

    clickByText(ctx.el, 'Następna');

    ctx.http.expectOne(r => r.url === url && r.params.get('page') === '2' && r.params.get('query') === '')
      .flush({ items: [], totalCount: 45, page: 2, pageSize: 20 });
  });

  describe('adding a mission', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj misję');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }
    const saveButton = (el: HTMLElement) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

    it('requires a catechist and posts the typed mission data', async () => {
      const ctx = await openForm();
      expect(saveButton(ctx.el).disabled).toBe(true);

      setSelect(ctx.el, 'select[name="personId"]', 'p1');
      setInput(ctx.el, 'input[name="servicePlace"]', 'Parafia św. Jana');
      setInput(ctx.el, 'input[name="missionStartDate"]', '2026-01-01');
      setInput(ctx.el, 'input[name="missionEndDate"]', '2029-01-01');
      setInput(ctx.el, 'input[name="grantedDate"]', '2025-12-20');
      setInput(ctx.el, 'input[name="grantedPlace"]', 'Bydgoszcz');
      setInput(ctx.el, 'input[name="supervisionGroup"]', 'Grupa B');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(false);

      saveButton(ctx.el).click();
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({
        personId: 'p1', servicePlace: 'Parafia św. Jana', missionStartDate: '2026-01-01', missionEndDate: '2029-01-01',
        grantedDate: '2025-12-20', grantedPlace: 'Bydgoszcz', supervisionGroup: 'Grupa B', sentToDok: false
      });
      req.flush(mission);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano misję.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush({ items: [mission], totalCount: 1, page: 1, pageSize: 20 });
    });

    it('shows a toast when saving fails and closes the form on cancel', async () => {
      const ctx = await openForm();
      setSelect(ctx.el, 'select[name="personId"]', 'p1');
      setInput(ctx.el, 'input[name="missionStartDate"]', '2026-01-01');
      setInput(ctx.el, 'input[name="missionEndDate"]', '2029-01-01');
      ctx.fixture.detectChanges();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać misji.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('deleting a mission', () => {
    it('deletes after confirmation and reloads', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      expect(confirmSpy).toHaveBeenCalledWith('Usunąć misję „Anna Maj”?');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/1`).flush(null);

      expect(toastMessages()).toContain('Misja usunięta.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
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

      expect(toastMessages()).toContain('Nie udało się usunąć misji.');
    });
  });

  describe('editing a mission', () => {
    const detailed: Mission = {
      ...mission, grantedDate: '2023-10-01', grantedPlace: 'Bydgoszcz', supervisionGroup: 'Grupa A', sentToDok: false
    };
    const saveButton = (el: HTMLElement) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

    async function openEdit() {
      const ctx = boot([detailed]);
      clickByText(ctx.el, 'Edytuj');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('opens the form filled with the mission, including the DOK flag', async () => {
      const ctx = await openEdit();

      expect(textOf(ctx.el)).toContain('Edytuj misję');
      expect((ctx.el.querySelector('select[name="personId"]') as HTMLSelectElement).value).toBe('p1');
      expect((ctx.el.querySelector('input[name="servicePlace"]') as HTMLInputElement).value).toBe('Parafia św. Mateusza');
      expect((ctx.el.querySelector('input[name="missionStartDate"]') as HTMLInputElement).value).toBe('2023-10-15');
      expect((ctx.el.querySelector('input[name="missionEndDate"]') as HTMLInputElement).value).toBe('2026-10-14');
      expect((ctx.el.querySelector('input[name="grantedDate"]') as HTMLInputElement).value).toBe('2023-10-01');
      expect((ctx.el.querySelector('input[name="grantedPlace"]') as HTMLInputElement).value).toBe('Bydgoszcz');
      expect((ctx.el.querySelector('input[name="sentToDok"]') as HTMLInputElement).checked).toBe(false);
    });

    it('saves the change with PUT, including the DOK flag, and reloads', async () => {
      const ctx = await openEdit();

      setInput(ctx.el, 'input[name="servicePlace"]', 'Parafia św. Jana');
      (ctx.el.querySelector('input[name="sentToDok"]') as HTMLInputElement).click();
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/1`);
      expect(req.request.body).toEqual({
        personId: 'p1', servicePlace: 'Parafia św. Jana', missionStartDate: '2023-10-15', missionEndDate: '2026-10-14',
        grantedDate: '2023-10-01', grantedPlace: 'Bydgoszcz', supervisionGroup: 'Grupa A', sentToDok: true
      });
      req.flush({ ...detailed, servicePlace: 'Parafia św. Jana', sentToDok: true });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Zapisano zmiany.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush({ items: [detailed], totalCount: 1, page: 1, pageSize: 20 });
    });

    it('shows a toast when saving the change fails and keeps the form open', async () => {
      const ctx = await openEdit();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 400, statusText: 'Bad Request' });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Nie udało się zapisać zmian.');
      expect(ctx.el.querySelector('.modal')).not.toBeNull();
    });

    it('switches back to adding after an edit was cancelled', async () => {
      const ctx = await openEdit();
      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Dodaj misję');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      expect(textOf(ctx.el)).toContain('Nowa misja');
      expect((ctx.el.querySelector('input[name="servicePlace"]') as HTMLInputElement).value).toBe('');
    });

    it('is not offered to users who cannot manage missions', () => {
      const ctx = setup(MissionsListComponent, { granted: ['Missions.View'] });
      ctx.fixture.detectChanges();
      ctx.http.expectOne(r => r.url === url).flush({ items: [detailed], totalCount: 1, page: 1, pageSize: 20 });
      ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
      ctx.http.expectOne(r => r.url === api('/api/parishes')).flush([]);
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).not.toContain('Edytuj');
    });
  });
});
