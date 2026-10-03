import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PermissionMatrix, RoleInfo } from './permissions.model';

@Injectable({ providedIn: 'root' })
export class PermissionsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/permissions`;

  constructor(private readonly http: HttpClient) {}

  getMatrix() {
    return this.http.get<PermissionMatrix>(`${this.baseUrl}/matrix`);
  }

  updateRole(role: string, permissions: string[]) {
    return this.http.put<void>(`${this.baseUrl}/roles/${encodeURIComponent(role)}`, { permissions });
  }

  createRole(name: string) {
    return this.http.post<RoleInfo>(`${this.baseUrl}/roles`, { name });
  }

  deleteRole(role: string) {
    return this.http.delete<void>(`${this.baseUrl}/roles/${encodeURIComponent(role)}`);
  }
}
