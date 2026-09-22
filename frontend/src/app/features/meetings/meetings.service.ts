import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CreateMeetingValue, Meeting } from './meeting.model';

@Injectable({ providedIn: 'root' })
export class MeetingsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/meetings`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<Meeting[]>(this.baseUrl);
  }

  create(value: CreateMeetingValue) {
    return this.http.post<Meeting>(this.baseUrl, value);
  }
}
