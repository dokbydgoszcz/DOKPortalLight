import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, expect, it } from 'vitest';
import { CandidatesService } from './candidates.service';
import { environment } from '../../../environments/environment';

describe('CandidatesService', () => {
  it('sends the year as a request parameter when searching', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CandidatesService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.search(3).subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/candidates` && r.params.get('year') === '3'
    );
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    httpMock.verify();
  });

  it('updates a candidate with PUT, sending the whole retreats list', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CandidatesService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.update('c1', { personId: 'p1', year: 2, opinionsCollected: 1, retreats: [{ year: 1, isCompleted: true }] }).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/candidates/c1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.retreats).toEqual([{ year: 1, isCompleted: true }]);
    req.flush({});
  });

  it('moves the selected candidates with one POST carrying their ids', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CandidatesService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.advance(['c1', 'c2']).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/candidates/advance`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ candidateIds: ['c1', 'c2'] });
    req.flush({ advanced: 2, completed: 0, skipped: [] });
  });
});
