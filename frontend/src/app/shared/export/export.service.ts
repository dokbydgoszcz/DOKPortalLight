import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ToastService } from '../../core/notifications/toast.service';

@Injectable({ providedIn: 'root' })
export class ExportService {
  constructor(
    private readonly http: HttpClient,
    private readonly toast: ToastService
  ) {}

  download(path: string, fileName: string): Observable<void> {
    return this.http.get(`${environment.apiBaseUrl}/api/export/${path}`, { responseType: 'blob' }).pipe(
      map(blob => this.save(blob, fileName)),
      catchError(() => {
        this.toast.error('Nie udało się pobrać pliku Excel.');
        return of(undefined);
      })
    );
  }

  private save(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }
}
