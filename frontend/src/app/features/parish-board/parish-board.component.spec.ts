import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { ParishBoardComponent } from './parish-board.component';
import { environment } from '../../../environments/environment';

describe('ParishBoardComponent', () => {
  let fixture: ComponentFixture<ParishBoardComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ParishBoardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(ParishBoardComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders parish needs returned from the API', () => {
    fixture.detectChanges();
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/parish-needs`).flush([
      { id: '1', parishId: 'par1', parishName: 'św. Mateusza', description: 'Katechista do przygotowania dorosłych', status: 'Open', assignedPersonId: null, assignedPersonName: null, assignedAtUtc: null }
    ]);
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/parishes`).flush([]);
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('św. Mateusza');
    expect(text).toContain('Katechista do przygotowania dorosłych');
  });
});
