import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { SupervisionsService } from './supervisions.service';
import { environment } from '../../../environments/environment';

describe('SupervisionsService', () => {
  it('requests supervisions from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(SupervisionsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/supervisions`);
    req.flush([]);
    httpMock.verify();
  });
});
