import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { DocumentsComponent } from './documents.component';
import { environment } from '../../../environments/environment';

describe('DocumentsComponent', () => {
  let fixture: ComponentFixture<DocumentsComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DocumentsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(DocumentsComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders document history returned from the API', () => {
    fixture.detectChanges();
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/documents`).flush([
      { id: '1', template: 'LetterToBishop', personId: 'p1', personFullName: 'Jan Kowalski', generatedByUserId: 'u1', additionalNotes: null, createdAtUtc: '2026-09-22T00:00:00Z' }
    ]);
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Jan Kowalski');
  });
});
