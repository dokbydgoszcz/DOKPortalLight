import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { SupervisionsListComponent } from './supervisions-list.component';
import { environment } from '../../../environments/environment';

describe('SupervisionsListComponent', () => {
  let fixture: ComponentFixture<SupervisionsListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SupervisionsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(SupervisionsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders supervisions returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/supervisions`);
    req.flush([{ id: '1', institution: 'DOK', groupLabel: 'Grupa A', supervisionDate: '2026-09-30', attendeesCount: 8, expectedCount: 8, topic: null, conclusion: null }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Grupa A');
  });
});
