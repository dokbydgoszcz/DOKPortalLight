import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MissionsListComponent } from './missions-list.component';
import { Mission } from './mission.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const mission: Mission = {
  id: '1', personId: 'p1', personFullName: 'Anna Maj', servicePlace: 'Parafia św. Mateusza',
  missionStartDate: '2023-10-15', missionEndDate: '2026-10-14', grantedDate: null, grantedPlace: null,
  supervisionGroup: 'Grupa A', status: 'wygasa'
};
const people = [
  { id: 'p1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];
const url = api('/api/missions');

function boot(items: Mission[] = [mission], totalCount = items.length) {
  const ctx = setup(MissionsListComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === url).flush({ items, totalCount, page: 1, pageSize: 20 });
  ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
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
        grantedDate: '2025-12-20', grantedPlace: 'Bydgoszcz', supervisionGroup: 'Grupa B'
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
});
