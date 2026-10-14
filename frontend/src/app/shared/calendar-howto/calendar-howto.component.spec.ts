import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { CalendarHowtoComponent } from './calendar-howto.component';

function render(linkToAccount: boolean) {
  TestBed.configureTestingModule({ imports: [CalendarHowtoComponent], providers: [provideRouter([])] });
  const fixture = TestBed.createComponent(CalendarHowtoComponent);
  fixture.componentRef.setInput('linkToAccount', linkToAccount);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('CalendarHowtoComponent', () => {
  it('walks through adding the calendar in Google, with a link to "Moje konto" outside it', () => {
    const el = render(true);
    expect(el.textContent).toContain('Utwórz adres kalendarza');
    expect(el.textContent).toContain('Z adresu URL');
    expect(el.textContent).toContain('iPhone');
    expect(el.querySelector('a')?.getAttribute('href')).toBe('/konto/haslo');
  });

  it('has no link inside "Moje konto" itself', () => {
    const el = render(false);
    expect(el.querySelector('a')).toBeNull();
    expect(el.textContent).toContain('Kliknij');
  });
});
