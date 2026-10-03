import { Provider, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { AuthService } from '../core/auth/auth.service';
import { environment } from '../../environments/environment';

export const api = (path: string): string => `${environment.apiBaseUrl}${path}`;

export function provideFakeAuth(granted: string[] = ['*']): Provider {
  const allowed = (permission: string) => granted.includes('*') || granted.includes(permission);
  return {
    provide: AuthService,
    useValue: {
      hasPermission: allowed,
      hasAnyPermission: (permissions: string[]) => permissions.some(allowed),
      roles: () => [],
      permissions: () => granted,
      token: null,
      logout: vi.fn()
    }
  };
}

export function setup<T>(
  component: Type<T>,
  options: { granted?: string[]; providers?: Provider[] } = {}
): { fixture: ComponentFixture<T>; http: HttpTestingController; el: HTMLElement } {
  TestBed.configureTestingModule({
    imports: [component],
    providers: [provideHttpClient(), provideHttpClientTesting(), provideFakeAuth(options.granted), ...(options.providers ?? [])]
  });
  const fixture = TestBed.createComponent(component);
  const http = TestBed.inject(HttpTestingController);
  return { fixture, http, el: fixture.nativeElement as HTMLElement };
}

export function paged<T>(items: T[], page = 1, pageSize = 20) {
  return { items, totalCount: items.length, page, pageSize };
}

export function flushAll(http: HttpTestingController, url: string, body: object | string | number | boolean | null): number {
  const requests = http.match(r => r.url === url);
  requests.forEach(r => r.flush(body));
  return requests.length;
}

export function click(el: HTMLElement, selector: string): void {
  const target = el.querySelector<HTMLElement>(selector);
  if (!target) throw new Error(`Brak elementu: ${selector}`);
  target.click();
}

export function clickByText(el: HTMLElement, text: string, selector = 'button, span, a, label'): void {
  const target = Array.from(el.querySelectorAll<HTMLElement>(selector)).find(e => (e.textContent ?? '').trim().includes(text));
  if (!target) throw new Error(`Brak elementu z tekstem „${text}” (${selector})`);
  target.click();
}

export function setInput(el: HTMLElement, selector: string, value: string): void {
  const input = el.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector);
  if (!input) throw new Error(`Brak pola: ${selector}`);
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

export function setSelect(el: HTMLElement, selector: string, value: string): void {
  const select = el.querySelector<HTMLSelectElement>(selector);
  if (!select) throw new Error(`Brak listy: ${selector}`);
  select.value = value;
  select.dispatchEvent(new Event('change'));
}

export function textOf(el: HTMLElement): string {
  return (el.textContent ?? '').replace(/\s+/g, ' ').trim();
}
