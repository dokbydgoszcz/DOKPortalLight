import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { PermissionsMatrixComponent } from './permissions-matrix.component';
import { ToastService } from '../../core/notifications/toast.service';
import { environment } from '../../../environments/environment';

const base = `${environment.apiBaseUrl}/api/permissions`;

const matrix = {
  roles: [
    { name: 'Biskup', isSystem: true },
    { name: 'Sekretariat', isSystem: false }
  ],
  permissions: [
    { name: 'People.Manage', module: 'Osoby', label: 'Dodawanie, edycja i usuwanie osób' },
    { name: 'People.Export', module: 'Osoby', label: 'Eksport osób do Excela' },
    { name: 'Meetings.View', module: 'Spotkania', label: 'Podgląd harmonogramu i obecności' }
  ],
  grants: { Biskup: ['Meetings.View'], Sekretariat: [] }
};

describe('PermissionsMatrixComponent', () => {
  let fixture: ComponentFixture<PermissionsMatrixComponent>;
  let httpMock: HttpTestingController;

  function render() {
    fixture.detectChanges();
    httpMock.expectOne(`${base}/matrix`).flush(matrix);
    fixture.detectChanges();
  }

  function checkbox(role: string, permission: string): HTMLInputElement {
    return fixture.nativeElement.querySelector(`input[data-role="${role}"][data-permission="${permission}"]`);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PermissionsMatrixComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(PermissionsMatrixComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders module headers, role columns and current grants', () => {
    render();
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Osoby');
    expect(text).toContain('Spotkania');
    expect(text).toContain('Biskup');
    expect(text).toContain('Sekretariat');
    expect(checkbox('Biskup', 'Meetings.View').checked).toBe(true);
    expect(checkbox('Sekretariat', 'Meetings.View').checked).toBe(false);
  });

  it('offers deletion only for custom roles', () => {
    render();

    expect(fixture.nativeElement.querySelector('[data-delete-role="Sekretariat"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-delete-role="Biskup"]')).toBeNull();
  });

  it('enables saving after a change and sends only the changed role', () => {
    render();
    const save = () => fixture.nativeElement.querySelector('button[data-save]') as HTMLButtonElement;
    expect(save().disabled).toBe(true);

    const box = checkbox('Sekretariat', 'People.Manage');
    box.checked = true;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(save().disabled).toBe(false);

    save().click();
    const req = httpMock.expectOne(`${base}/roles/Sekretariat`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ permissions: ['People.Manage'] });
    req.flush(null);
    httpMock.expectOne(`${base}/matrix`).flush({ ...matrix, grants: { Biskup: ['Meetings.View'], Sekretariat: ['People.Manage'] } });
    httpMock.expectNone(`${base}/roles/Biskup`);
  });

  it('reverts unsaved changes', () => {
    render();
    const box = checkbox('Biskup', 'People.Export');
    box.checked = true;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[data-discard]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(checkbox('Biskup', 'People.Export').checked).toBe(false);
    expect((fixture.nativeElement.querySelector('button[data-save]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('creates a role from the name field and reloads the matrix', () => {
    render();
    const input = fixture.nativeElement.querySelector('input[data-new-role]') as HTMLInputElement;
    input.value = 'Archiwum';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[data-create-role]') as HTMLButtonElement).click();

    const req = httpMock.expectOne(`${base}/roles`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Archiwum' });
    req.flush({ name: 'Archiwum', isSystem: false });
    httpMock.expectOne(`${base}/matrix`).flush(matrix);
  });

  it('shows a toast when the matrix cannot be loaded', () => {
    fixture.detectChanges();

    httpMock.expectOne(`${base}/matrix`).flush('x', { status: 500, statusText: 'Server Error' });

    expect(TestBed.inject(ToastService).toasts().map(t => t.message)).toContain('Nie udało się wczytać uprawnień.');
  });

  it('shows the reason from the server when saving, creating or deleting fails, and reloads after a failed save', () => {
    render();
    const messages = () => TestBed.inject(ToastService).toasts().map(t => t.message);
    const box = checkbox('Sekretariat', 'People.Manage');
    box.checked = true;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[data-save]') as HTMLButtonElement).click();
    httpMock.expectOne(`${base}/roles/Sekretariat`).flush({ title: 'Nieznane uprawnienie: People.Manage.' }, { status: 400, statusText: 'Bad Request' });
    expect(messages()).toContain('Nieznane uprawnienie: People.Manage.');
    httpMock.expectOne(`${base}/matrix`).flush(matrix);
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input[data-new-role]') as HTMLInputElement;
    input.value = 'Biskup';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('button[data-create-role]') as HTMLButtonElement).click();
    httpMock.expectOne(`${base}/roles`).flush({ title: 'Rola „Biskup” już istnieje.' }, { status: 400, statusText: 'Bad Request' });
    expect(messages()).toContain('Rola „Biskup” już istnieje.');

    vi.spyOn(window, 'confirm').mockReturnValue(true);
    (fixture.nativeElement.querySelector('[data-delete-role="Sekretariat"]') as HTMLElement).click();
    httpMock.expectOne(`${base}/roles/Sekretariat`).flush('x', { status: 500, statusText: 'Server Error' });
    expect(messages()).toContain('Nie udało się usunąć roli.');
  });

  it('does not delete a role when the confirmation is cancelled and ignores an empty role name', () => {
    render();
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    (fixture.nativeElement.querySelector('[data-delete-role="Sekretariat"]') as HTMLElement).click();
    fixture.componentInstance.newRoleName = '   ';
    fixture.componentInstance.createRole();
    fixture.componentInstance.save();

    httpMock.expectNone(r => r.method === 'DELETE' || r.method === 'POST' || r.method === 'PUT');
  });

  it('deletes a custom role after confirmation', () => {
    render();
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    (fixture.nativeElement.querySelector('[data-delete-role="Sekretariat"]') as HTMLElement).click();

    const req = httpMock.expectOne(`${base}/roles/Sekretariat`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    httpMock.expectOne(`${base}/matrix`).flush(matrix);
  });
});
