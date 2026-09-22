import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Formator, FormatorFormValue } from './formator.model';

@Injectable({ providedIn: 'root' })
export class FormatorsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/formators`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<Formator[]>(this.baseUrl);
  }

  create(value: FormatorFormValue) {
    return this.http.post<Formator>(this.baseUrl, value);
  }
}
