import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { NameDaysService } from './name-days.service';
import { environment } from '../../../environments/environment';

describe('NameDaysService', () => {
  it('requests upcoming namedays from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(NameDaysService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.upcoming(30).subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/name-days/upcoming`);
    req.flush([]);
    httpMock.verify();
  });
});
