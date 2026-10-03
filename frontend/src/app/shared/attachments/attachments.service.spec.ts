import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { AttachmentsService } from './attachments.service';
import { Attachment } from './attachment.model';

const base = 'http://api.test/api/supervisions/s1/attachments';
const attachment: Attachment = { id: 'a1', fileName: 'protokol.pdf', contentType: 'application/pdf', sizeBytes: 100, uploadedAtUtc: '2026-10-03T10:00:00Z' };
const pdf = (name = 'protokol.pdf') => new File(['abc'], name, { type: 'application/pdf' });

describe('AttachmentsService', () => {
  let service: AttachmentsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AttachmentsService);
    http = TestBed.inject(HttpTestingController);
  });

  it('uploads one file as form data', () => {
    const file = pdf();

    service.upload(base, file).subscribe();

    const req = http.expectOne(r => r.method === 'POST' && r.url === base);
    expect((req.request.body as FormData).get('file')).toBe(file);
    req.flush(attachment);
  });

  it('downloads a file as a blob and deletes by id', () => {
    service.download(base, 'a1').subscribe();
    const download = http.expectOne(r => r.method === 'GET' && r.url === `${base}/a1/download`);
    expect(download.request.responseType).toBe('blob');
    download.flush(new Blob(['x']));

    service.remove(base, 'a1').subscribe();
    http.expectOne(r => r.method === 'DELETE' && r.url === `${base}/a1`).flush(null, { status: 204, statusText: 'No Content' });
  });

  describe('uploadMany', () => {
    it('uploads the files one after another and counts them', async () => {
      const result = firstValueFrom(service.uploadMany(base, [pdf('a.pdf'), pdf('b.pdf')]));

      http.expectOne(r => r.method === 'POST').flush(attachment);
      http.expectOne(r => r.method === 'POST').flush(attachment);

      expect(await result).toEqual({ uploaded: 2, errors: [] });
    });

    it('skips files that break the rules without calling the API, and says why', async () => {
      const result = firstValueFrom(service.uploadMany(base, [pdf('a.pdf'), new File(['x'], 'virus.exe')]));

      http.expectOne(r => r.method === 'POST').flush(attachment);

      const summary = await result;
      expect(summary.uploaded).toBe(1);
      expect(summary.errors).toHaveLength(1);
      expect(summary.errors[0]).toContain('virus.exe');
      expect(summary.errors[0]).toContain('Niedozwolony typ pliku');
    });

    it('keeps going after a failed upload and reports the server message', async () => {
      const result = firstValueFrom(service.uploadMany(base, [pdf('a.pdf'), pdf('b.pdf'), pdf('c.pdf')]));

      http.expectOne(r => r.method === 'POST').flush(attachment);
      http.expectOne(r => r.method === 'POST').flush({ title: 'Plik jest za duży – maksymalny rozmiar to 20 MB.' }, { status: 400, statusText: 'Bad Request' });
      http.expectOne(r => r.method === 'POST').flush('x', { status: 500, statusText: 'Server Error' });

      const summary = await result;
      expect(summary.uploaded).toBe(1);
      expect(summary.errors).toEqual([
        'b.pdf: Plik jest za duży – maksymalny rozmiar to 20 MB.',
        'c.pdf: Nie udało się przesłać pliku.'
      ]);
    });

    it('finishes at once for an empty selection', async () => {
      expect(await firstValueFrom(service.uploadMany(base, []))).toEqual({ uploaded: 0, errors: [] });
    });
  });
});
