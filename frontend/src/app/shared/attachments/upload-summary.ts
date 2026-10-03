import { OperatorFunction, reduce } from 'rxjs';
import { UploadSummary } from './attachment.model';

/** Wynik wysłania jednego pliku: brak błędu albo komunikat dla użytkownika. */
export interface UploadOutcome {
  fileName: string;
  error: string | null;
}

/** Zbiera wyniki pojedynczych plików w jedno podsumowanie; dla pustej listy daje zerowy wynik. */
export function toSummary(): OperatorFunction<UploadOutcome, UploadSummary> {
  return reduce<UploadOutcome, UploadSummary>(
    (summary, outcome) =>
      outcome.error
        ? { ...summary, errors: [...summary.errors, `${outcome.fileName}: ${outcome.error}`] }
        : { ...summary, uploaded: summary.uploaded + 1 },
    { uploaded: 0, errors: [] }
  );
}
