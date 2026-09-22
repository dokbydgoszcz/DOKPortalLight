import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { MissionsListComponent } from './missions-list.component';
import { environment } from '../../../environments/environment';

describe('MissionsListComponent', () => {
  let fixture: ComponentFixture<MissionsListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MissionsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(MissionsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders missions returned from the API with their status', () => {
    fixture.detectChanges();
    const missionsReq = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/missions`);
    missionsReq.flush({
      items: [{ id: '1', personId: 'p1', personFullName: 'Anna Maj', servicePlace: 'Parafia św. Mateusza', missionStartDate: '2023-10-15', missionEndDate: '2026-10-14', grantedDate: null, grantedPlace: null, supervisionGroup: 'Grupa A', status: 'wygasa' }],
      totalCount: 1, page: 1, pageSize: 100
    });
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Anna Maj');
    expect(text).toContain('wygasa');
  });
});
