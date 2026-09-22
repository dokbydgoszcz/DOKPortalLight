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
}
