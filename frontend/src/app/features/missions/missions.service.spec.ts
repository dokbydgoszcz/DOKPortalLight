import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, expect, it } from 'vitest';
import { MissionsService } from './missions.service';
import { environment } from '../../../environments/environment';

describe('MissionsService', () => {
  it('requests missions from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(MissionsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.search().subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/missions`);
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    httpMock.verify();
  });

  it('updates a mission with PUT', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(MissionsService);
    const httpMock = TestBed.inject(HttpTestingController);
    const value = { personId: 'p1', servicePlace: 'Parafia', missionStartDate: '2026-01-01', missionEndDate: '2029-01-01', sentToDok: true };

    service.update('m1', value).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/missions/m1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(value);
    req.flush({});
    httpMock.verify();
  });

  it('loads the people waiting for the mission and grants it with one POST', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(MissionsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.pending().subscribe();
    const pending = httpMock.expectOne(`${environment.apiBaseUrl}/api/missions/pending`);
    expect(pending.request.method).toBe('GET');
    pending.flush([]);

    service.grant('p1').subscribe();
    const grant = httpMock.expectOne(`${environment.apiBaseUrl}/api/missions/grant`);
    expect(grant.request.method).toBe('POST');
    expect(grant.request.body).toEqual({ personId: 'p1' });
    grant.flush({});
  });
});
