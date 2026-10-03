import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { CaseDocumentsService } from './case-documents.service';
import { environment } from '../../../environments/environment';

describe('CaseDocumentsService', () => {
  let service: CaseDocumentsService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/dok-cases/c1/documents`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(CaseDocumentsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('lists and creates documents of a case', () => {
    service.list('c1').subscribe();
    expect(httpMock.expectOne(base).request.method).toBe('GET');

    service.create('c1', 'Metryka chrztu').subscribe();
    const create = httpMock.expectOne(base);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'Metryka chrztu' });
  });

  it('marks a document as provided with PUT', () => {
    service.setProvided('c1', 'd1', true).subscribe();

    const req = httpMock.expectOne(`${base}/d1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ isProvided: true });
  });

  it('uploads a file as multipart form data', () => {
    const file = new File(['abc'], 'metryka.pdf');

    service.upload('c1', 'd1', file).subscribe();

    const req = httpMock.expectOne(`${base}/d1/upload`);
    expect(req.request.method).toBe('POST');
    expect((req.request.body as FormData).get('file')).toBe(file);
  });

  it('downloads a file as a blob response', () => {
    service.download('c1', 'd1').subscribe();

    const req = httpMock.expectOne(`${base}/d1/download`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
  });

  describe('uploadAsNewPositions', () => {
    it('creates a position named after the file and uploads the file into it, file after file', async () => {
      const result = firstValueFrom(service.uploadAsNewPositions('c1', [new File(['a'], 'Metryka chrztu.pdf'), new File(['b'], 'Akt.png')]));

      const first = httpMock.expectOne(base);
      expect(first.request.body).toEqual({ name: 'Metryka chrztu' });
      first.flush({ id: 'n1' });
      httpMock.expectOne(`${base}/n1/upload`).flush({});
      const second = httpMock.expectOne(base);
      expect(second.request.body).toEqual({ name: 'Akt' });
      second.flush({ id: 'n2' });
      httpMock.expectOne(`${base}/n2/upload`).flush({});

      expect(await result).toEqual({ uploaded: 2, errors: [] });
    });

    it('skips files that break the rules and reports a failed step with its message', async () => {
      const result = firstValueFrom(service.uploadAsNewPositions('c1', [new File(['x'], 'virus.exe'), new File(['b'], 'a.pdf')]));

      httpMock.expectOne(base).flush({ id: 'n1' });
      httpMock.expectOne(`${base}/n1/upload`).flush({ title: 'Plik jest pusty.' }, { status: 400, statusText: 'Bad Request' });

      const summary = await result;
      expect(summary.uploaded).toBe(0);
      expect(summary.errors[0]).toContain('virus.exe: Niedozwolony typ pliku');
      expect(summary.errors[1]).toBe('a.pdf: Plik jest pusty.');
    });
  });
});
