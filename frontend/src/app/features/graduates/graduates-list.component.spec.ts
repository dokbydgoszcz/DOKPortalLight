import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { GraduatesListComponent } from './graduates-list.component';
import { environment } from '../../../environments/environment';

describe('GraduatesListComponent', () => {
  let fixture: ComponentFixture<GraduatesListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [GraduatesListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(GraduatesListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders only cases whose stage is Graduate', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/dok-cases`);
    req.flush({
      items: [
        { id: '1', personId: 'p1', personFullName: 'Katarzyna Jankowska', parishName: 'św. Mateusza', path: 'BaptismCandidate', stage: 'Graduate', catechistPersonId: 'c1', catechistFullName: 'Joanna Lis', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: '2026-04-04T00:00:00Z' },
        { id: '2', personId: 'p2', personFullName: 'Jan Kowalski', parishName: null, path: 'Confirmation', stage: 'Formation', catechistPersonId: 'c2', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null }
      ],
      totalCount: 2, page: 1, pageSize: 100
    });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Katarzyna Jankowska');
    expect(text).not.toContain('Jan Kowalski');
  });
});
