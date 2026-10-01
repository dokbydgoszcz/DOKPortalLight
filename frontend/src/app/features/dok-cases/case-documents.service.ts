import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CaseDocument } from './case-document.model';

@Injectable({ providedIn: 'root' })
export class CaseDocumentsService {
  constructor(private readonly http: HttpClient) {}

  private baseUrl(caseId: string) {
    return `${environment.apiBaseUrl}/api/dok-cases/${caseId}/documents`;
  }

  list(caseId: string) {
    return this.http.get<CaseDocument[]>(this.baseUrl(caseId));
  }

  create(caseId: string, name: string) {
    return this.http.post<CaseDocument>(this.baseUrl(caseId), { name });
  }

  setProvided(caseId: string, id: string, isProvided: boolean) {
    return this.http.put<CaseDocument>(`${this.baseUrl(caseId)}/${id}`, { isProvided });
  }

  upload(caseId: string, id: string, file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<CaseDocument>(`${this.baseUrl(caseId)}/${id}/upload`, formData);
  }

  download(caseId: string, id: string) {
    return this.http.get(`${this.baseUrl(caseId)}/${id}/download`, { responseType: 'blob', observe: 'response' });
  }
}
