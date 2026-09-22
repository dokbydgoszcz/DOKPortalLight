import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { PeopleService } from './people.service';
import { environment } from '../../../environments/environment';

describe('PeopleService', () => {
  it('sends the query as a request parameter when searching', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(PeopleService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.search('Kowalski').subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/people` && r.params.get('query') === 'Kowalski'
    );
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    httpMock.verify();
  });
});
