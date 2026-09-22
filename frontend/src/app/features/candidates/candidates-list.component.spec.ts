import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { CandidatesListComponent } from './candidates-list.component';
import { environment } from '../../../environments/environment';

describe('CandidatesListComponent', () => {
  let fixture: ComponentFixture<CandidatesListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CandidatesListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(CandidatesListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('shows the per-year candidate count computed from the fetched list', () => {
    fixture.detectChanges();
    const candidatesReq = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/candidates`);
    candidatesReq.flush({
      items: [
        { id: '1', personId: 'p1', personFullName: 'Agnieszka Lewandowska', parishName: null, year: 3, attendancePercentage: 94, opinionsCollected: 2, opinionsRequired: 2, isRetreatCompleted: true },
        { id: '2', personId: 'p2', personFullName: 'Karolina Nowak', parishName: null, year: 1, attendancePercentage: 81, opinionsCollected: 0, opinionsRequired: 2, isRetreatCompleted: false }
      ],
      totalCount: 2, page: 1, pageSize: 100
    });
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Agnieszka Lewandowska');
    expect(text).toContain('Karolina Nowak');
  });
});
