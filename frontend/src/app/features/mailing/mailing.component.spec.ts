import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { MailingComponent } from './mailing.component';
import { environment } from '../../../environments/environment';

describe('MailingComponent', () => {
  let fixture: ComponentFixture<MailingComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MailingComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(MailingComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders campaigns returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/mailing/campaigns`);
    req.flush([{ id: '1', subject: 'Zaproszenie', body: 'Treść', group: 'DokCases', recipientCount: 12, status: 'Draft', createdAtUtc: '2026-09-22T00:00:00Z', sentAtUtc: null }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Zaproszenie');
  });
});
