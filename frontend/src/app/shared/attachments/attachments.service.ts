import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, concatMap, from, map, of } from 'rxjs';
import { Attachment, UploadSummary } from './attachment.model';
import { validateFile } from './attachment-rules';
import { UploadOutcome, serverMessage, toSummary } from './upload-summary';

/** Pliki dołączone do notatek i superwizji; adres bazowy (…/attachments) podaje właściciel załączników. */
@Injectable({ providedIn: 'root' })
export class AttachmentsService {
  constructor(private readonly http: HttpClient) {}

  upload(baseUrl: string, file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<Attachment>(baseUrl, formData);
  }

  download(baseUrl: string, id: string) {
    return this.http.get(`${baseUrl}/${id}/download`, { responseType: 'blob' });
  }

  remove(baseUrl: string, id: string) {
    return this.http.delete<void>(`${baseUrl}/${id}`);
  }

  /**
   * Wysyła pliki po kolei. Plik niezgodny z zasadami nie jest wysyłany, a błąd jednego pliku nie przerywa pozostałych.
   * Nigdy nie kończy się błędem – wynik opisuje, ile plików poszło i co się nie udało.
   */
  uploadMany(baseUrl: string, files: File[]): Observable<UploadSummary> {
    return from(files).pipe(
      concatMap(file => this.uploadOne(baseUrl, file)),
      toSummary()
    );
  }

  private uploadOne(baseUrl: string, file: File): Observable<UploadOutcome> {
    const invalid = validateFile(file);
    if (invalid) return of({ fileName: file.name, error: invalid });

    return this.upload(baseUrl, file).pipe(
      map((): UploadOutcome => ({ fileName: file.name, error: null })),
      catchError(err => of<UploadOutcome>({ fileName: file.name, error: serverMessage(err, 'Nie udało się przesłać pliku.') }))
    );
  }
}
