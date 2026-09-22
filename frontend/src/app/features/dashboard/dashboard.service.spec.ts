import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { DashboardService } from './dashboard.service';
import { environment } from '../../../environments/environment';

describe('DashboardService', () => {
  it('requests the summary from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(DashboardService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.getSummary().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/dashboard/summary`);
    req.flush({ peopleCount: 1, parishCount: 1 });
    httpMock.verify();
  });
});
