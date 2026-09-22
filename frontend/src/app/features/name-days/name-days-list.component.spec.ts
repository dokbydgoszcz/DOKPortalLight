import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { NameDaysListComponent } from './name-days-list.component';
import { environment } from '../../../environments/environment';

describe('NameDaysListComponent', () => {
  let fixture: ComponentFixture<NameDaysListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [NameDaysListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(NameDaysListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders upcoming namedays returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/name-days/upcoming`);
    req.flush([{ personId: '1', fullName: 'Ewa Nowak', nameDayMonth: 12, nameDayDay: 24, daysUntil: 3 }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Ewa Nowak');
  });
});
