import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
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
});
