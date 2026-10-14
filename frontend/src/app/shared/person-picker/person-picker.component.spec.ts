import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PersonPickerComponent } from './person-picker.component';
import { api, paged, pickPerson, setInput, setup } from '../../testing/test-helpers';

@Component({
  standalone: true,
  imports: [FormsModule, PersonPickerComponent],
  template: `<app-person-picker name="who" [(ngModel)]="value" [exclude]="exclude()" [fn]="fn" [disabled]="locked" />`
})
class HostComponent {
  value = '';
  locked = false;
  fn: 'Pastor' | undefined = undefined;
  readonly exclude = signal<string[]>([]);
}

const peopleUrl = api('/api/people');
const anna = { id: 'p1', fullName: 'Anna Maj', email: 'anna@example.org' };
const jan = { id: 'p2', fullName: 'Jan Kowalski', email: null };
const host = 'app-person-picker[name="who"]';

async function boot(configure?: (h: HostComponent) => void) {
  const ctx = setup(HostComponent);
  configure?.(ctx.fixture.componentInstance);
  ctx.fixture.detectChanges();
  await ctx.fixture.whenStable();
  ctx.fixture.detectChanges();
  return ctx;
}

const input = (el: HTMLElement) => el.querySelector<HTMLInputElement>(`${host} input`)!;
const options = (el: HTMLElement) => Array.from(el.querySelectorAll<HTMLElement>(`${host} li`)).map(li => li.textContent!.trim());
const key = (el: HTMLElement, k: string) => input(el).dispatchEvent(new KeyboardEvent('keydown', { key: k }));

describe('PersonPickerComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('searches as you type and sets the chosen person id', async () => {
    const ctx = await boot();

    pickPerson(ctx, host, anna);

    expect(ctx.fixture.componentInstance.value).toBe('p1');
    expect(input(ctx.el).value).toBe('Anna Maj');
    expect(ctx.el.querySelector(`${host} ul`)).toBeNull();
  });

  it('shows the e-mail next to the name and says when nobody matches', async () => {
    const ctx = await boot();
    setInput(ctx.el, `${host} input`, 'Ma');
    ctx.fixture.detectChanges();
    expect(options(ctx.el)).toEqual(['Szukam…']);
    ctx.http.expectOne(r => r.url === peopleUrl && r.params.get('query') === 'Ma').flush(paged([anna]));
    ctx.fixture.detectChanges();
    expect(options(ctx.el)[0]).toContain('anna@example.org');

    setInput(ctx.el, `${host} input`, 'Xyz');
    ctx.http.expectOne(r => r.params.get('query') === 'Xyz').flush(paged([]));
    ctx.fixture.detectChanges();
    expect(options(ctx.el)).toEqual(['Nikogo nie znaleziono.']);
  });

  it('shows the name of an already chosen person and unsets it when the text is edited', async () => {
    const ctx = await boot(h => (h.value = 'p2'));
    ctx.http.expectOne(`${peopleUrl}/p2`).flush(jan);
    ctx.fixture.detectChanges();
    expect(input(ctx.el).value).toBe('Jan Kowalski');

    setInput(ctx.el, `${host} input`, 'Jan Ko');
    expect(ctx.fixture.componentInstance.value).toBe('');
    ctx.http.expectOne(r => r.params.get('query') === 'Jan Ko').flush(paged([jan]));
  });

  it('says when the chosen person cannot be loaded', async () => {
    const ctx = await boot(h => (h.value = 'gone'));
    ctx.http.expectOne(`${peopleUrl}/gone`).flush('x', { status: 404, statusText: 'Not Found' });
    ctx.fixture.detectChanges();
    expect(input(ctx.el).value).toBe('(osoba niedostępna)');
  });

  it('clears the choice with the × button', async () => {
    const ctx = await boot();
    pickPerson(ctx, host, anna);

    ctx.el.querySelector<HTMLButtonElement>(`${host} .picker-clear`)!.click();
    ctx.fixture.detectChanges();

    expect(ctx.fixture.componentInstance.value).toBe('');
    expect(input(ctx.el).value).toBe('');
  });

  it('lists people on focus, filtered by function and without excluded ones', async () => {
    const ctx = await boot(h => {
      h.fn = 'Pastor';
      h.exclude.set(['p2']);
    });

    input(ctx.el).dispatchEvent(new Event('focus'));
    const req = ctx.http.expectOne(r => r.url === peopleUrl);
    expect(req.request.params.get('function')).toBe('Pastor');
    expect(req.request.params.get('pageSize')).toBe('16');
    req.flush(paged([anna, jan]));
    ctx.fixture.detectChanges();

    expect(options(ctx.el).map(o => o.split('anna')[0].trim())).toEqual(['Anna Maj']);
  });

  it('picks with the arrow keys and Enter, closes with Escape', async () => {
    const ctx = await boot();
    key(ctx.el, 'ArrowDown');
    ctx.http.expectOne(r => r.url === peopleUrl).flush(paged([anna, jan]));
    ctx.fixture.detectChanges();

    key(ctx.el, 'ArrowDown');
    key(ctx.el, 'ArrowDown');
    key(ctx.el, 'ArrowUp');
    key(ctx.el, 'ArrowUp');
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector(`${host} li.active`)!.textContent).toContain('Jan Kowalski');
    key(ctx.el, 'Enter');
    ctx.fixture.detectChanges();
    expect(ctx.fixture.componentInstance.value).toBe('p2');

    key(ctx.el, 'ArrowDown');
    ctx.http.expectOne(r => r.url === peopleUrl).flush(paged([]));
    key(ctx.el, 'ArrowDown');
    key(ctx.el, 'Enter');
    key(ctx.el, 'Escape');
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector(`${host} ul`)).toBeNull();
  });

  it('closes the list when clicking outside and survives a failed search', async () => {
    const ctx = await boot();
    setInput(ctx.el, `${host} input`, 'An');
    ctx.http.expectOne(r => r.url === peopleUrl).flush('x', { status: 500, statusText: 'Server Error' });
    ctx.fixture.detectChanges();
    expect(options(ctx.el)).toEqual(['Nikogo nie znaleziono.']);

    input(ctx.el).click();
    document.body.click();
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector(`${host} ul`)).toBeNull();
    document.body.click();
  });

  it('can be disabled and reset from the form', async () => {
    const ctx = await boot(h => (h.locked = true));
    expect(input(ctx.el).disabled).toBe(true);

    const picker = ctx.fixture.debugElement.children[0].componentInstance as PersonPickerComponent;
    picker.writeValue(null);
    picker.writeValue('p1');
    ctx.http.expectOne(`${peopleUrl}/p1`).flush(anna);
    picker.writeValue('p1');
    picker.writeValue('');
    expect(picker.text()).toBe('');
  });

  it('uses the name from the suggestions without asking the server again, and keeps focus on mouse press', async () => {
    const ctx = await boot();
    setInput(ctx.el, `${host} input`, 'Anna');
    ctx.http.expectOne(r => r.params.get('query') === 'Anna').flush(paged([anna]));
    ctx.fixture.detectChanges();
    const li = ctx.el.querySelector<HTMLElement>(`${host} li[role="option"]`)!;
    const press = new MouseEvent('mousedown', { cancelable: true });
    li.dispatchEvent(press);
    expect(press.defaultPrevented).toBe(true);

    const picker = ctx.fixture.debugElement.children[0].componentInstance as PersonPickerComponent;
    picker.writeValue('p1');
    ctx.http.expectNone(`${peopleUrl}/p1`);
    expect(picker.text()).toBe('Anna Maj');
  });

  it('works without a form binding', () => {
    const ctx = setup(PersonPickerComponent);
    ctx.fixture.detectChanges();
    ctx.fixture.componentInstance.choose({ id: 'p1', fullName: 'Anna Maj' } as never);
    ctx.fixture.componentInstance.clear();
    expect(ctx.fixture.componentInstance.selectedId()).toBe('');
  });
});
