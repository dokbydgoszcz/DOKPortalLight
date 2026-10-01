import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { Mission, MissionFormValue } from './mission.model';

@Injectable({ providedIn: 'root' })
export class MissionsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/missions`;

  constructor(private readonly http: HttpClient) {}

  search(query = '', page = 1, pageSize = 20) {
    return this.http.get<PagedResult<Mission>>(this.baseUrl, { params: { query, page, pageSize } });
  }

  create(value: MissionFormValue) {
    return this.http.post<Mission>(this.baseUrl, value);
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
