import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { DashboardComponent } from './dashboard.component';
import { environment } from '../../../environments/environment';

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
      providers: [provideHttpClient(), provideHttpClientTesting()]
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
});
