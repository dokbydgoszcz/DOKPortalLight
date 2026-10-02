import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ExportButtonComponent } from './export-button.component';
import { AuthService } from '../../core/auth/auth.service';
import { environment } from '../../../environments/environment';

describe('ExportButtonComponent', () => {
  let fixture: ComponentFixture<ExportButtonComponent>;
  let httpMock: HttpTestingController;

  function setup(allowed: boolean) {
    TestBed.configureTestingModule({
      imports: [ExportButtonComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { hasAnyRole: () => allowed } }
      ]
    });
    fixture = TestBed.createComponent(ExportButtonComponent);
    fixture.componentRef.setInput('list', 'people');
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => 'blob:test');
    URL.revokeObjectURL = vi.fn();
  });

  it('is hidden for users without an allowed role', () => {
    setup(false);

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });

  it('downloads the list when clicked by an allowed user', () => {
    setup(true);
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    fixture.nativeElement.querySelector('button').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button').disabled).toBe(true);
    httpMock.expectOne(`${environment.apiBaseUrl}/api/export/people`).flush(new Blob(['x']));
    fixture.detectChanges();

    expect(click).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.querySelector('button').disabled).toBe(false);
    click.mockRestore();
  });
});
