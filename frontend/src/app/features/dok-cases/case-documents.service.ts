import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable, catchError, concatMap, from, map, of, switchMap } from 'rxjs';
import { CaseDocument } from './case-document.model';
import { UploadSummary } from '../../shared/attachments/attachment.model';
import { documentNameFromFile, validateFile } from '../../shared/attachments/attachment-rules';
import { UploadOutcome, toSummary } from '../../shared/attachments/upload-summary';
import { serverMessage } from '../../shared/http-error';

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

  /**
   * Dla każdego pliku zakłada pozycję na liście (nazwa z nazwy pliku) i od razu wysyła do niej plik – jeden po drugim.
   * Nigdy nie kończy się błędem; wynik mówi, ile plików poszło i co się nie udało.
   */
  uploadAsNewPositions(caseId: string, files: File[]): Observable<UploadSummary> {
    return from(files).pipe(
      concatMap(file => this.createWithFile(caseId, file)),
      toSummary()
    );
  }

  private createWithFile(caseId: string, file: File): Observable<UploadOutcome> {
    const invalid = validateFile(file);
    if (invalid) return of({ fileName: file.name, error: invalid });

    return this.create(caseId, documentNameFromFile(file.name)).pipe(
      switchMap(document => this.upload(caseId, document.id, file)),
      map((): UploadOutcome => ({ fileName: file.name, error: null })),
      catchError(err => of<UploadOutcome>({ fileName: file.name, error: serverMessage(err, 'Nie udało się przesłać pliku.') }))
    );
  }

  download(caseId: string, id: string) {
    return this.http.get(`${this.baseUrl(caseId)}/${id}/download`, { responseType: 'blob', observe: 'response' });
  }
}
