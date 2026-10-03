import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, expect, it } from 'vitest';
import { AuditLogService } from './audit-log.service';
import { environment } from '../../../environments/environment';

describe('AuditLogService', () => {
  it('requests audit log entries from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuditLogService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/audit-log`);
    req.flush([]);
    httpMock.verify();
  });

  it('sends only the filled filters as query parameters, plus the limit', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuditLogService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list({ search: ' anna ', action: '', result: 'Blocked', from: '2026-10-01', to: '', take: 100 }).subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/audit-log`);
    expect(req.request.params.get('search')).toBe('anna');
    expect(req.request.params.get('result')).toBe('Blocked');
    expect(req.request.params.get('from')).toBe('2026-10-01');
    expect(req.request.params.get('take')).toBe('100');
    expect(req.request.params.has('action')).toBe(false);
    expect(req.request.params.has('to')).toBe(false);
    req.flush([]);
    httpMock.verify();
  });

  it('requests the list of action names', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuditLogService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.actions().subscribe();

    httpMock.expectOne(`${environment.apiBaseUrl}/api/audit-log/actions`).flush([]);
    httpMock.verify();
  });
});
