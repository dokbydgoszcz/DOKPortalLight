import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AppUserAccount, CreateUserValue } from './user.model';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/users`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<AppUserAccount[]>(this.baseUrl);
  }

  create(value: CreateUserValue) {
    return this.http.post<AppUserAccount>(this.baseUrl, value);
  }

  assignRoles(id: string, roles: string[]) {
    return this.http.put<AppUserAccount>(`${this.baseUrl}/${id}/roles`, { roles });
  }
}
