import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Parish } from './parish-need.model';

@Injectable({ providedIn: 'root' })
export class ParishesService {
  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<Parish[]>(`${environment.apiBaseUrl}/api/parishes`);
  }

  create(value: { name: string; city?: string }) {
    return this.http.post<Parish>(`${environment.apiBaseUrl}/api/parishes`, value);
  }

  update(id: string, value: { name: string; city?: string }) {
    return this.http.put<Parish>(`${environment.apiBaseUrl}/api/parishes/${id}`, value);
  }

  delete(id: string) {
    return this.http.delete<void>(`${environment.apiBaseUrl}/api/parishes/${id}`);
  }
}
