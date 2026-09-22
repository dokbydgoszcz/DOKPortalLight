import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { DokCasesListComponent } from './dok-cases-list.component';
import { environment } from '../../../environments/environment';

describe('DokCasesListComponent', () => {
  let fixture: ComponentFixture<DokCasesListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DokCasesListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(DokCasesListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('shows the per-path count computed from the fetched list', () => {
    fixture.detectChanges();
    const casesReq = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/dok-cases`);
    casesReq.flush({
      items: [
        { id: '1', personId: 'p1', personFullName: 'Jan Kowalski', parishName: null, path: 'Confirmation', stage: 'Formation', catechistPersonId: 'c1', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null },
        { id: '2', personId: 'p2', personFullName: 'Piotr Malinowski', parishName: null, path: 'Conversion', stage: 'Sacrament', catechistPersonId: 'c2', catechistFullName: 'Maria Kaczmarek', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null }
      ],
      totalCount: 2, page: 1, pageSize: 100
    });
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Jan Kowalski');
    expect(text).toContain('Piotr Malinowski');
  });
});
