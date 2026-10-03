import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { PermissionsService } from './permissions.service';
import { environment } from '../../../environments/environment';

describe('PermissionsService', () => {
  let service: PermissionsService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/permissions`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PermissionsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('loads the matrix', () => {
    service.getMatrix().subscribe();

    httpMock.expectOne(`${base}/matrix`).flush({ roles: [], permissions: [], grants: {} });
  });

  it('saves role permissions with the role name url-encoded', () => {
    service.updateRole('Rola testowa', ['People.Manage']).subscribe();

    const req = httpMock.expectOne(`${base}/roles/Rola%20testowa`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ permissions: ['People.Manage'] });
    req.flush(null);
  });

  it('creates and deletes roles', () => {
    service.createRole('Sekretariat').subscribe();
    const create = httpMock.expectOne(`${base}/roles`);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'Sekretariat' });
    create.flush({ name: 'Sekretariat', isSystem: false });

    service.deleteRole('Sekretariat').subscribe();
    const del = httpMock.expectOne(`${base}/roles/Sekretariat`);
    expect(del.request.method).toBe('DELETE');
    del.flush(null);
  });
});
