import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { UpcomingNameDay } from './name-day.model';

@Injectable({ providedIn: 'root' })
export class NameDaysService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/name-days`;

  constructor(private readonly http: HttpClient) {}

  upcoming(days = 30) {
    return this.http.get<UpcomingNameDay[]>(`${this.baseUrl}/upcoming`, { params: { days } });
  }
}
