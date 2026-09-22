import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { FormatorsService } from './formators.service';
import { environment } from '../../../environments/environment';

describe('FormatorsService', () => {
  it('requests the formators list from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(FormatorsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/formators`);
    req.flush([]);
    httpMock.verify();
  });
});
