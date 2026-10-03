import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, expect, it } from 'vitest';
import { ParishNeedsService } from './parish-needs.service';
import { environment } from '../../../environments/environment';

describe('ParishNeedsService', () => {
  it('requests the needs list from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(ParishNeedsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/parish-needs`);
    req.flush([]);
    httpMock.verify();
  });

  it('updates a need with PUT and unassigns a person with DELETE', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(ParishNeedsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.update('n1', { parishId: 'pa1', description: 'Dwóch katechistów' }).subscribe();
    const update = httpMock.expectOne(`${environment.apiBaseUrl}/api/parish-needs/n1`);
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ parishId: 'pa1', description: 'Dwóch katechistów' });
    update.flush({});

    service.unassign('n1', 'p1').subscribe();
    const unassign = httpMock.expectOne(`${environment.apiBaseUrl}/api/parish-needs/n1/assign/p1`);
    expect(unassign.request.method).toBe('DELETE');
    unassign.flush({});
  });
});
