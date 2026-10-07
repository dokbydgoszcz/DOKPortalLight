import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { ResourcesService } from './resources.service';
import { environment } from '../../../environments/environment';

describe('ResourcesService', () => {
  let service: ResourcesService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/resources`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ResourcesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('lists with an optional query', () => {
    service.list('wzory').subscribe();
    service.list().subscribe();

    expect(httpMock.expectOne(r => r.url === base && r.params.get('query') === 'wzory').request.method).toBe('GET');
    expect(httpMock.expectOne(r => r.url === base && !r.params.has('query')).request.method).toBe('GET');
    httpMock.verify();
  });

  it('creates, updates and deletes with the matching HTTP methods', () => {
    service.create({ title: 'A' }).subscribe();
    expect(httpMock.expectOne(base).request.method).toBe('POST');
    service.update('1', { title: 'B' }).subscribe();
    expect(httpMock.expectOne(`${base}/1`).request.method).toBe('PUT');
    service.delete('1').subscribe();
    expect(httpMock.expectOne(`${base}/1`).request.method).toBe('DELETE');
  });

  it('builds the attachments address of a resource', () => {
    expect(service.attachmentsUrl('r1')).toBe(`${base}/r1/attachments`);
  });
});
