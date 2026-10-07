import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { DashboardComponent } from './dashboard.component';
import { environment } from '../../../environments/environment';
import { provideFakeAuth } from '../../testing/test-helpers';

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let httpMock: HttpTestingController;

  const summary = {
    peopleCount: 12,
    parishCount: 3,
    activeCandidatesCount: 7,
    upcomingMeetingsCount: 5,
    missingDocumentsCasesCount: 4,
    dokCasesByStage: [
      { stage: 'Evangelization', count: 11 },
      { stage: 'CloserFormation', count: 22 },
      { stage: 'Prekatechumenate', count: 33 },
      { stage: 'Catechumenate', count: 44 },
      { stage: 'Election', count: 55 },
      { stage: 'Neophyte', count: 66 },
      { stage: 'Graduate', count: 77 }
    ],
    stalledCases: [] as unknown[],
    stalledCasesCount: 0
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideFakeAuth()]
    });
    fixture = TestBed.createComponent(DashboardComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders the people count returned by the API', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiBaseUrl}/api/dashboard/summary`).flush(summary);
    fixture.detectChanges();

    expect((fixture.nativeElement.textContent as string)).toContain('12');
  });

  it('renders candidates, meetings, missing documents and DOK cases per stage', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiBaseUrl}/api/dashboard/summary`).flush(summary);
    fixture.detectChanges();

    const stats = new Map<string, string>(
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.stat')).map(card => [
        card.querySelector('.stat-label')!.textContent!.trim(),
        card.querySelector('.stat-value')!.textContent!.trim()
      ])
    );
    expect(stats.get('Kandydaci SKŚP')).toBe('7');
    expect(stats.get('Spotkania w ciągu 7 dni')).toBe('5');
    expect(stats.get('Sprawy DOK z brakującymi dokumentami')).toBe('4');
    expect(stats.get('DOK — Ewangelizacja')).toBe('11');
    expect(stats.get('DOK — Formacja bliższa')).toBe('22');
    expect(stats.get('DOK — Prekatechumenat')).toBe('33');
    expect(stats.get('DOK — Katechumenat')).toBe('44');
    expect(stats.get('DOK — Wybranie')).toBe('55');
    expect(stats.get('DOK — Neofita')).toBe('66');
    expect(stats.get('DOK — Absolwent')).toBe('77');
  });

  describe('cases that stay on one stage for over a year', () => {
    const stalled = (name: string, stage: string, since: string, months: number, path = 'BaptismCandidate') =>
      ({ caseId: name, personFullName: name, path, stage, stageSinceUtc: since, monthsOnStage: months });

    function renderWith(cases: unknown[], count = cases.length) {
      fixture.detectChanges();
      httpMock.expectOne(`${environment.apiBaseUrl}/api/dashboard/summary`).flush({ ...summary, stalledCases: cases, stalledCasesCount: count });
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    it('shows nothing when no case is stuck', () => {
      const el = renderWith([]);

      expect(el.querySelector('.attention')).toBeNull();
    });

    it('lists each stuck case with its path, stage and how long it has been there', () => {
      const el = renderWith([
        stalled('Jan Kowalski', 'Catechumenate', '2024-03-01T00:00:00Z', 31),
        stalled('Anna Maj', 'Evangelization', '2025-09-01T00:00:00Z', 13, 'Communion')
      ]);

      const card = el.querySelector('.attention') as HTMLElement;
      const text = (card.textContent ?? '').replace(/\s+/g, ' ');
      expect(text).toContain('Wymaga uwagi');
      expect(text).toContain('Jan Kowalski — Kandydaci do Chrztu, Katechumenat');
      expect(text).toContain('od 01.03.2024 (31 mies.)');
      expect(text).toContain('Anna Maj — Eucharystia, Ewangelizacja');
      expect(card.querySelectorAll('.list-row')).toHaveLength(2);
    });

    it('says how many more there are than the list shows', () => {
      const el = renderWith([stalled('Jan Kowalski', 'Catechumenate', '2024-03-01T00:00:00Z', 31)], 27);

      const text = (el.querySelector('.attention')!.textContent ?? '').replace(/\s+/g, ' ');
      expect(text).toContain('(27)');
      expect(text).toContain('i 26 więcej');
    });
  });

  describe('upcoming name days', () => {
    const day = (fullName: string, daysUntil: number, month = 10, dayOfMonth = 9) =>
      ({ personId: fullName, fullName, nameDayMonth: month, nameDayDay: dayOfMonth, daysUntil });

    function renderNameDays(items: unknown[] | 'error') {
      fixture.detectChanges();
      httpMock.expectOne(`${environment.apiBaseUrl}/api/dashboard/summary`).flush(summary);
      const request = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/name-days/upcoming`);
      expect(request.request.params.get('days')).toBe('14');
      if (items === 'error') request.flush('x', { status: 500, statusText: 'Server Error' });
      else request.flush(items);
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    it('lists who has a name day in the next two weeks, with the date and how soon', () => {
      const el = renderNameDays([day('Jan Kowalski', 0, 10, 7), day('Anna Maj', 1, 10, 8), day('Piotr Nowak', 5, 10, 12)]);

      const card = el.querySelector('.name-days') as HTMLElement;
      const rows = Array.from(card.querySelectorAll('.list-row')).map(r =>
        `${r.querySelector('.list-title')!.textContent!.trim()} ${r.querySelector('.list-sub')!.textContent!.trim()}`);
      expect(card.textContent).toContain('Nadchodzące imieniny');
      expect(rows).toEqual([
        'Jan Kowalski 07.10 — dziś',
        'Anna Maj 08.10 — jutro',
        'Piotr Nowak 12.10 — za 5 dni'
      ]);
    });

    it('says so when nobody has a name day soon', () => {
      const el = renderNameDays([]);

      expect((el.querySelector('.name-days')!.textContent ?? '')).toContain('W najbliższych 14 dniach nikt nie ma imienin.');
    });

    it('leaves the rest of the dashboard alone when the name days cannot be loaded', () => {
      const el = renderNameDays('error');

      expect(el.querySelector('.name-days')).toBeNull();
      expect(el.textContent).toContain('12');
    });
  });

  describe('lists and links', () => {
    const meeting = (id: string, date: string, label: string) => ({ meetingId: id, meetingDate: date, label });
    const missing = (name: string, docs: string[], path = 'Confirmation') => ({ caseId: name, personFullName: name, path, missingDocuments: docs });

    function render(extra: object = {}, granted?: string[]) {
      if (granted) {
        TestBed.resetTestingModule();
        TestBed.configureTestingModule({
          imports: [DashboardComponent],
          providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideFakeAuth(granted)]
        });
        fixture = TestBed.createComponent(DashboardComponent);
        httpMock = TestBed.inject(HttpTestingController);
      }
      fixture.detectChanges();
      httpMock.expectOne(`${environment.apiBaseUrl}/api/dashboard/summary`).flush({ ...summary, ...extra });
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    const tile = (el: HTMLElement, label: string) =>
      Array.from(el.querySelectorAll<HTMLElement>('.stat')).find(c => c.querySelector('.stat-label')!.textContent!.trim() === label)!;

    it('lists the meetings of the next 7 days, each leading to the schedule', () => {
      const el = render({
        upcomingMeetingsCount: 12,
        upcomingMeetings: [meeting('m1', '2026-10-08', 'Jan Kowalski'), meeting('m2', '2026-10-09', 'Grupa wtorkowa')]
      });

      const card = el.querySelector('.meetings') as HTMLElement;
      const rows = Array.from(card.querySelectorAll<HTMLElement>('.list-row'));
      expect(card.textContent).toContain('Spotkania w najbliższych 7 dniach (12)');
      expect(rows.map(r => r.querySelector('.list-title')!.textContent!.trim())).toEqual(['Jan Kowalski', 'Grupa wtorkowa']);
      expect(rows[0].querySelector('.list-sub')!.textContent).toContain('08.10.2026');
      expect(rows.every(r => r.getAttribute('href') === '/meetings')).toBe(true);
      expect(card.textContent).toContain('…i 10 więcej');
    });

    it('lists the DOK cases with missing documents and what is missing, each leading to the cases list', () => {
      const el = render({
        missingDocumentsCasesCount: 2,
        missingDocumentsCases: [missing('Jan Kowalski', ['Akt urodzenia', 'Zaświadczenie']), missing('Marek Zieliński', ['Metryka chrztu'], 'BaptismCandidate')]
      });

      const card = el.querySelector('.missing-documents') as HTMLElement;
      const rows = Array.from(card.querySelectorAll<HTMLElement>('.list-row'));
      expect(card.textContent).toContain('Sprawy DOK z brakującymi dokumentami (2)');
      expect(rows[0].querySelector('.list-title')!.textContent).toContain('Jan Kowalski — Bierzmowanie');
      expect(rows[0].querySelector('.list-sub')!.textContent).toContain('brakuje: Akt urodzenia, Zaświadczenie');
      expect(rows[1].querySelector('.list-title')!.textContent).toContain('Marek Zieliński — Kandydaci do Chrztu');
      expect(rows.every(r => r.getAttribute('href') === '/dok-cases')).toBe(true);
    });

    it('shows neither list when there is nothing to list', () => {
      const el = render({ upcomingMeetings: [], missingDocumentsCases: [] });

      expect(el.querySelector('.meetings')).toBeNull();
      expect(el.querySelector('.missing-documents')).toBeNull();
    });

    it('turns the count tiles into links to the matching screens', () => {
      const el = render();

      expect(tile(el, 'Osoby w bazie').getAttribute('href')).toBe('/people');
      expect(tile(el, 'Parafie').getAttribute('href')).toBe('/parishes');
      expect(tile(el, 'Kandydaci SKŚP').getAttribute('href')).toBe('/candidates');
      expect(tile(el, 'Spotkania w ciągu 7 dni').getAttribute('href')).toBe('/meetings');
      expect(tile(el, 'Sprawy DOK z brakującymi dokumentami').getAttribute('href')).toBe('/dok-cases');
    });

    it('sends a DOK stage tile to the cases list filtered by that stage', () => {
      const el = render();

      expect(tile(el, 'DOK — Ewangelizacja').getAttribute('href')).toBe('/dok-cases?stage=Evangelization');
      expect(tile(el, 'DOK — Neofita').getAttribute('href')).toBe('/dok-cases?stage=Neophyte');
    });

    it('does not link to screens the user may not open', () => {
      const el = render({}, ['Meetings.View']);

      expect(tile(el, 'Osoby w bazie').getAttribute('href')).toBe('/people');
      expect(tile(el, 'Spotkania w ciągu 7 dni').getAttribute('href')).toBe('/meetings');
      expect(tile(el, 'Parafie').hasAttribute('href')).toBe(false);
      expect(tile(el, 'Kandydaci SKŚP').hasAttribute('href')).toBe(false);
      expect(tile(el, 'DOK — Ewangelizacja').hasAttribute('href')).toBe(false);
    });
  });
});
