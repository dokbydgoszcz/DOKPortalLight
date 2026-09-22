import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { DocumentsService } from './documents.service';
import { environment } from '../../../environments/environment';

describe('DocumentsService', () => {
  it('requests document history from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(DocumentsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.history().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/documents`);
    req.flush([]);
    httpMock.verify();
  });

  it('posts a generate request expecting a blob response', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(DocumentsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.generate({ template: 'LetterToBishop', personId: 'p1' }).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/documents/generate`);
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob());
    httpMock.verify();
  });
});
