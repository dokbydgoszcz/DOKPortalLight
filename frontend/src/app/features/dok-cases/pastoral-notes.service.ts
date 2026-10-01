import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PastoralNote } from './pastoral-note.model';

@Injectable({ providedIn: 'root' })
export class PastoralNotesService {
  constructor(private readonly http: HttpClient) {}

  private baseUrl(caseId: string) {
    return `${environment.apiBaseUrl}/api/dok-cases/${caseId}/notes`;
  }

  list(caseId: string) {
    return this.http.get<PastoralNote[]>(this.baseUrl(caseId));
  }

  create(caseId: string, content: string) {
    return this.http.post<PastoralNote>(this.baseUrl(caseId), { content });
  }
}
