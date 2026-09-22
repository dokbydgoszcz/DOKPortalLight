import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
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
});
