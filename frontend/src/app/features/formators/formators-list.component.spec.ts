import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { FormatorsListComponent } from './formators-list.component';
import { environment } from '../../../environments/environment';

describe('FormatorsListComponent', () => {
  let fixture: ComponentFixture<FormatorsListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FormatorsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(FormatorsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders formators returned from the API', () => {
    fixture.detectChanges();
    const formatorsReq = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/formators`);
    formatorsReq.flush([{ id: '1', personId: 'p1', personFullName: 'Joanna Lis', personEmail: 'j.lis@example.org', personPhone: null, function: 'Wykładowca' }]);
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Joanna Lis');
    expect(text).toContain('Wykładowca');
  });
});
