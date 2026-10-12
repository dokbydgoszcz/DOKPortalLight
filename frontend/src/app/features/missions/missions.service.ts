import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { Mission, MissionFormValue, PendingCatechist } from './mission.model';

@Injectable({ providedIn: 'root' })
export class MissionsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/missions`;

  constructor(private readonly http: HttpClient) {}

  /** status: Current = ważne, Expired = wygasłe; bez = wszystkie (ważne na górze). */
  search(query = '', page = 1, pageSize = 20, status?: 'Current' | 'Expired') {
    const params: Record<string, string | number> = { query, page, pageSize };
    if (status) params['status'] = status;
    return this.http.get<PagedResult<Mission>>(this.baseUrl, { params });
  }

  /** Misje kończące się w ciągu 30 dni albo wygasłe w ostatnim roku, bez późniejszej misji tej osoby. */
  expiring() {
    return this.http.get<Mission[]>(`${this.baseUrl}/expiring`);
  }

  /** Kolejna misja na rok od dnia po końcu tej (najwcześniej od dziś), z tym samym miejscem i grupą. */
  renew(id: string) {
    return this.http.post<Mission>(`${this.baseUrl}/${id}/renew`, null);
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
