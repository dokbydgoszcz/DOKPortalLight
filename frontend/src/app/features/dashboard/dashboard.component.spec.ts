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
      { stage: 'Application', count: 11 },
      { stage: 'Formation', count: 22 },
      { stage: 'Sacrament', count: 33 },
      { stage: 'Graduate', count: 44 }
    ]
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
    expect(stats.get('DOK — Zgłoszenie')).toBe('11');
    expect(stats.get('DOK — Formacja')).toBe('22');
    expect(stats.get('DOK — Sakrament')).toBe('33');
    expect(stats.get('DOK — Absolwent')).toBe('44');
  });
});
