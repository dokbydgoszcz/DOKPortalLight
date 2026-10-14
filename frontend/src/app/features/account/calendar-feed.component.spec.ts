import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CalendarFeedComponent } from './calendar-feed.component';
import { ToastService } from '../../core/notifications/toast.service';
import { api, setup, textOf } from '../../testing/test-helpers';

const url = api('/api/calendar/feed');
const address = 'https://api.example.org/api/calendar/abc.ics';
const toasts = () => TestBed.inject(ToastService).toasts().map(t => t.message);

async function boot(current: string | null) {
  const ctx = setup(CalendarFeedComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush({ url: current });
  await ctx.fixture.whenStable();
  ctx.fixture.detectChanges();
  return ctx;
}

describe('CalendarFeedComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('creates the private address and explains how to add it to Google Calendar', async () => {
    const ctx = await boot(null);
    expect(textOf(ctx.el)).toContain('Utwórz adres kalendarza');

    const created = ctx.fixture.componentInstance.create();
    ctx.http.expectOne(r => r.method === 'POST' && r.url === url).flush({ url: address });
    await created;
    ctx.fixture.detectChanges();

    expect((ctx.el.querySelector('.address input') as HTMLInputElement).value).toBe(address);
    expect(textOf(ctx.el)).toContain('Z adresu URL');
  });

  it('makes a new address and turns it off after confirmation', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValue(true);
    const ctx = await boot(address);
    const component = ctx.fixture.componentInstance;

    await component.regenerate();
    ctx.http.expectNone(r => r.method === 'POST');
    const again = component.regenerate();
    ctx.http.expectOne(r => r.method === 'POST').flush({ url: address + '2' });
    await again;
    expect(component.url()).toBe(address + '2');

    const off = component.disable();
    ctx.http.expectOne(r => r.method === 'DELETE').flush(null);
    await off;
    expect(component.url()).toBeNull();
    expect(toasts()).toContain('Adres kalendarza wyłączony.');
    expect(confirmSpy).toHaveBeenCalledTimes(3);
  });

  it('reports failures', async () => {
    const ctx = setup(CalendarFeedComponent);
    ctx.fixture.detectChanges();
    ctx.http.expectOne(url).flush('x', { status: 500, statusText: 'Error' });
    await ctx.fixture.whenStable();
    const failed = ctx.fixture.componentInstance.create();
    ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 500, statusText: 'Error' });
    await failed;

    expect(toasts()).toEqual(expect.arrayContaining(['Nie udało się sprawdzić adresu kalendarza.', 'Nie udało się utworzyć adresu kalendarza.']));
  });

  it('copies the address', async () => {
    const ctx = await boot(address);
    const write = vi.fn().mockResolvedValueOnce(undefined).mockRejectedValueOnce(new Error('no'));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: write }, configurable: true });

    await ctx.fixture.componentInstance.copy(address);
    await ctx.fixture.componentInstance.copy(address);

    expect(write).toHaveBeenCalledWith(address);
    expect(toasts()).toEqual(expect.arrayContaining(['Skopiowano adres kalendarza.', 'Nie udało się skopiować – zaznacz adres i skopiuj ręcznie.']));
  });
});
