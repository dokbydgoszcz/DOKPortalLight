import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { Mission, MissionFormValue, PendingCatechist } from './mission.model';

@Injectable({ providedIn: 'root' })
export class MissionsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/missions`;

  constructor(private readonly http: HttpClient) {}

  search(query = '', page = 1, pageSize = 20) {
    return this.http.get<PagedResult<Mission>>(this.baseUrl, { params: { query, page, pageSize } });
  }

  pending() {
    return this.http.get<PendingCatechist[]>(`${this.baseUrl}/pending`);
  }

  /** Udziela posłania jednym ruchem; miejsce i daty uzupełnia się potem w edycji. */
  grant(personId: string) {
    return this.http.post<Mission>(`${this.baseUrl}/grant`, { personId });
  }

  create(value: MissionFormValue) {
    return this.http.post<Mission>(this.baseUrl, value);
  }

  update(id: string, value: MissionFormValue) {
    return this.http.put<Mission>(`${this.baseUrl}/${id}`, value);
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
