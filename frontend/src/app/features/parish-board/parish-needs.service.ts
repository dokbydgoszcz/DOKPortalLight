import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CreateParishNeedValue, ParishNeed } from './parish-need.model';

@Injectable({ providedIn: 'root' })
export class ParishNeedsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/parish-needs`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<ParishNeed[]>(this.baseUrl);
  }

  create(value: CreateParishNeedValue) {
    return this.http.post<ParishNeed>(this.baseUrl, value);
  }

  assign(id: string, personId: string) {
    return this.http.put<ParishNeed>(`${this.baseUrl}/${id}/assign`, { personId });
  }
}
