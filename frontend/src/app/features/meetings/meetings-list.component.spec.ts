import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { MeetingsListComponent } from './meetings-list.component';
import { environment } from '../../../environments/environment';

describe('MeetingsListComponent', () => {
  let fixture: ComponentFixture<MeetingsListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MeetingsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(MeetingsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders meetings returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/meetings`);
    req.flush([{ id: '1', dokCaseId: null, caseLabel: null, groupLabel: 'DOK grupa', meetingDate: '2026-09-24', isAttended: null, notes: null }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('DOK grupa');
  });
});
