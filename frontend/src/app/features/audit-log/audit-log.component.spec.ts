import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { AuditLogComponent } from './audit-log.component';
import { environment } from '../../../environments/environment';

describe('AuditLogComponent', () => {
  let fixture: ComponentFixture<AuditLogComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AuditLogComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(AuditLogComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders audit log entries returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/audit-log`);
    req.flush([{ id: '1', timestampUtc: '2026-09-22T10:00:00Z', userId: 'u1', userEmail: 'kat@example.org', action: 'ReadPastoralNotes', objectDescription: 'Jan Kowalski', result: 'Blocked' }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('kat@example.org');
    expect(text).toContain('Zablokowano');
  });
});
