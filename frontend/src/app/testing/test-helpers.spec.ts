import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { api, click, clickByText, flushAll, paged, provideFakeAuth, setInput, setSelect, setup, textOf } from './test-helpers';
import { AuthService } from '../core/auth/auth.service';

@Component({
  standalone: true,
  imports: [FormsModule],
  template: `
    <button id="a" (click)="clicks = clicks + 1">Zapisz zmiany</button>
    <span class="x" (click)="clicks = clicks + 10">Usuń</span>
    <input id="name" [(ngModel)]="name" />
    <select id="kind" [(ngModel)]="kind"><option value="a">A</option><option value="b">B</option></select>
    <p>{{ name }}   {{ kind }}</p>
  `
})
class HostComponent {
  clicks = 0;
  name = '';
  kind = 'a';
}

describe('test helpers', () => {
  it('builds api urls and paged payloads', () => {
    expect(api('/api/people')).toContain('/api/people');
    expect(paged([1, 2, 3])).toEqual({ items: [1, 2, 3], totalCount: 3, page: 1, pageSize: 20 });
  });

  it('provides a fake auth that grants everything or only the listed permissions', () => {
    TestBed.configureTestingModule({ providers: [provideFakeAuth(['People.Manage'])] });
    const auth = TestBed.inject(AuthService);

    expect(auth.hasPermission('People.Manage')).toBe(true);
    expect(auth.hasPermission('People.Export')).toBe(false);
    expect(auth.hasAnyPermission(['People.Export', 'People.Manage'])).toBe(true);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideFakeAuth()] });
    expect(TestBed.inject(AuthService).hasPermission('Anything.Goes')).toBe(true);
  });

  it('drives a component through click, input and select helpers', () => {
    const { fixture, el } = setup(HostComponent);
    fixture.detectChanges();

    click(el, '#a');
    clickByText(el, 'Usuń');
    setInput(el, '#name', 'Ola');
    setSelect(el, '#kind', 'b');
    fixture.detectChanges();

    expect(fixture.componentInstance.clicks).toBe(11);
    expect(textOf(el)).toContain('Ola b');
  });

  it('prefers an exact text match over a wrapper that merely contains the text', () => {
    @Component({
      standalone: true,
      template: `<span class="wrap"><span id="n" (click)="hit = 'n'">Notatki</span> <span id="d" (click)="hit = 'd'">Dokumenty</span></span>`
    })
    class WrapperHost {
      hit = '';
    }
    const { fixture, el } = setup(WrapperHost);
    fixture.detectChanges();

    clickByText(el, 'Dokumenty');

    expect(fixture.componentInstance.hit).toBe('d');
  });

  it('throws a readable error for missing elements', () => {
    const { fixture, el } = setup(HostComponent);
    fixture.detectChanges();

    expect(() => click(el, '#nope')).toThrow('Brak elementu: #nope');
    expect(() => clickByText(el, 'Nie ma')).toThrow('Brak elementu z tekstem');
    expect(() => setInput(el, '#nope', 'x')).toThrow('Brak pola');
    expect(() => setSelect(el, '#nope', 'x')).toThrow('Brak listy');
  });

  it('flushes every pending request for a url', () => {
    const { http } = setup(HostComponent);
    const client = TestBed.inject(HttpClient);
    const results: unknown[] = [];
    client.get(api('/api/x')).subscribe(r => results.push(r));
    client.get(api('/api/x')).subscribe(r => results.push(r));

    const flushed = flushAll(http, api('/api/x'), { ok: true });

    expect(flushed).toBe(2);
    expect(results).toEqual([{ ok: true }, { ok: true }]);
  });
});
