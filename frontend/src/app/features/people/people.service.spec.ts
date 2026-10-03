import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { PeopleService } from './people.service';
import { environment } from '../../../environments/environment';

describe('PeopleService', () => {
  let service: PeopleService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/people`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PeopleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('sends the query as a request parameter when searching', () => {
    service.search('Kowalski').subscribe();

    const req = httpMock.expectOne(r => r.url === base && r.params.get('query') === 'Kowalski');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    httpMock.verify();
  });

  it('loads, creates, updates and deletes a person with the matching HTTP methods', () => {
    service.getById('1').subscribe();
    expect(httpMock.expectOne(`${base}/1`).request.method).toBe('GET');

    service.create({ firstName: 'Ola', lastName: 'Nowak' }).subscribe();
    const create = httpMock.expectOne(base);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ firstName: 'Ola', lastName: 'Nowak' });

    service.update('1', { firstName: 'Ola', lastName: 'Maj' }).subscribe();
    const update = httpMock.expectOne(`${base}/1`);
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ firstName: 'Ola', lastName: 'Maj' });

    service.delete('1').subscribe();
    expect(httpMock.expectOne(`${base}/1`).request.method).toBe('DELETE');
  });
});
