import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
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
});
