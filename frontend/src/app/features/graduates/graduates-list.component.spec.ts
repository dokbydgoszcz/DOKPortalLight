import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { GraduatesListComponent } from './graduates-list.component';
import { DokCase } from '../dok-cases/dok-case.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, setInput, setup, textOf } from '../../testing/test-helpers';

const katarzyna: DokCase = {
  id: '1', personId: 'p1', personFullName: 'Katarzyna Jankowska', parishName: 'św. Mateusza', path: 'BaptismCandidate',
  stage: 'Graduate', catechistPersonId: 'c1', catechistFullName: 'Joanna Lis', mentorPersonId: null, mentorFullName: 'Piotr Nowak',
  lastMeetingDate: null, completedAtUtc: '2026-04-04T12:00:00Z',
  meetingsRecorded: 0, meetingsAttended: 0
};
const graduatesUrl = api('/api/graduates');

function boot(items: DokCase[] = [katarzyna], totalCount = items.length) {
  const ctx = setup(GraduatesListComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === graduatesUrl).flush({ items, totalCount, page: 1, pageSize: 20 });
  ctx.fixture.detectChanges();
  return ctx;
}

describe('GraduatesListComponent', () => {
  it('renders graduates returned by the graduates endpoint', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('Katarzyna Jankowska');
    expect(textOf(el)).toContain('św. Mateusza');
    expect(textOf(el)).toContain('Piotr Nowak');
  });

  it('shows the completion date as a date, not a raw timestamp', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('04.04.2026');
    expect(textOf(el)).not.toContain('2026-04-04T');
  });

  it('shows an empty-state message when there are no graduates', () => {
    const { el } = boot([]);

    expect(textOf(el)).toContain('Brak absolwentów.');
  });

  it('shows a toast when the list cannot be loaded', () => {
    const { fixture, http } = setup(GraduatesListComponent);
    fixture.detectChanges();

    http.expectOne(r => r.url === graduatesUrl).flush('x', { status: 500, statusText: 'Server Error' });

    expect(TestBed.inject(ToastService).toasts().map(t => t.message)).toContain('Nie udało się wczytać listy absolwentów.');
  });

  it('loads the next page from the server', () => {
    const ctx = boot([katarzyna], 45);

    clickByText(ctx.el, 'Następna');

    const req = ctx.http.expectOne(r => r.url === graduatesUrl && r.params.get('page') === '2');
    expect(req.request.params.get('pageSize')).toBe('20');
    req.flush({ items: [katarzyna], totalCount: 45, page: 2, pageSize: 20 });
  });

  it('searches from the first page with the typed query', () => {
    const ctx = boot([katarzyna], 45);
    clickByText(ctx.el, 'Następna');
    ctx.http.expectOne(r => r.url === graduatesUrl && r.params.get('page') === '2').flush({ items: [katarzyna], totalCount: 45, page: 2, pageSize: 20 });
    ctx.fixture.detectChanges();

    setInput(ctx.el, 'input[placeholder="Szukaj…"]', 'Jankowska');

    const req = ctx.http.expectOne(r => r.url === graduatesUrl && r.params.get('search') === 'Jankowska');
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ items: [katarzyna], totalCount: 1, page: 1, pageSize: 20 });
  });
});
