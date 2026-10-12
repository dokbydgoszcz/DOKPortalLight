import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { HandedOverCase, NoteSharing, PastoralNote } from './pastoral-note.model';

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

  update(caseId: string, noteId: string, content: string) {
    return this.http.put<PastoralNote>(`${this.baseUrl(caseId)}/${noteId}`, { content });
  }

  delete(caseId: string, noteId: string) {
    return this.http.delete<void>(`${this.baseUrl(caseId)}/${noteId}`);
  }

  sharing(caseId: string) {
    return this.http.get<NoteSharing>(`${this.baseUrl(caseId)}/sharing`);
  }

  /** „Udostępnij moje notatki” obecnemu katechiście prowadzącemu sprawę. */
  share(caseId: string) {
    return this.http.post<NoteSharing>(`${this.baseUrl(caseId)}/share`, null);
  }

  unshare(caseId: string, personId: string) {
    return this.http.delete<NoteSharing>(`${this.baseUrl(caseId)}/share/${personId}`);
  }

  /** Sprawy z moimi notatkami, które prowadzi już ktoś inny. */
  handedOver() {
    return this.http.get<HandedOverCase[]>(`${environment.apiBaseUrl}/api/pastoral-notes/handed-over`);
  }
}
