import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ExportService } from './export.service';
import { ToastService } from '../../core/notifications/toast.service';
import { environment } from '../../../environments/environment';

describe('ExportService', () => {
  let service: ExportService;
  let httpMock: HttpTestingController;
  let toast: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ExportService);
    httpMock = TestBed.inject(HttpTestingController);
    toast = TestBed.inject(ToastService);
    URL.createObjectURL = vi.fn(() => 'blob:test');
    URL.revokeObjectURL = vi.fn();
  });

  it('downloads the file as a blob and saves it under the given name', () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    service.download('people', 'osoby.xlsx').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/export/people`);
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['x']));

    expect(URL.createObjectURL).toHaveBeenCalled();
    expect(click).toHaveBeenCalledTimes(1);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
    click.mockRestore();
  });

  it('shows an error toast and completes when the request fails', () => {
    let completed = false;

    service.download('people', 'osoby.xlsx').subscribe({ complete: () => (completed = true) });

    httpMock
      .expectOne(`${environment.apiBaseUrl}/api/export/people`)
      .error(new ProgressEvent('error'), { status: 500, statusText: 'Server Error' });

    expect(completed).toBe(true);
    expect(toast.toasts()[0].kind).toBe('error');
  });
});
