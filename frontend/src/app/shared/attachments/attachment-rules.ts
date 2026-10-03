/** Te same zasady plików co po stronie serwera (AttachmentRules): dozwolone rozszerzenia i limit rozmiaru. */
const ALLOWED_EXTENSIONS = ['.pdf', '.jpg', '.jpeg', '.png', '.docx', '.doc', '.txt'];

export const MAX_FILE_BYTES = 20 * 1024 * 1024;
export const ALLOWED_TYPES_LABEL = 'PDF, JPG/JPEG, PNG, DOCX, DOC, TXT';
export const ACCEPT_ATTRIBUTE = ALLOWED_EXTENSIONS.join(',');
export const RULES_HINT = `Dozwolone: ${ALLOWED_TYPES_LABEL}, do 20 MB.`;

function extensionOf(fileName: string): string {
  const dot = fileName.lastIndexOf('.');
  return dot < 0 ? '' : fileName.slice(dot).toLowerCase();
}

/** Zwraca komunikat błędu albo null, gdy plik spełnia zasady. */
export function validateFile(file: Pick<File, 'name' | 'size'>): string | null {
  if (!ALLOWED_EXTENSIONS.includes(extensionOf(file.name))) {
    return `Niedozwolony typ pliku. Dozwolone: ${ALLOWED_TYPES_LABEL}.`;
  }
  if (file.size <= 0) return 'Plik jest pusty.';
  if (file.size > MAX_FILE_BYTES) return 'Plik jest za duży – maksymalny rozmiar to 20 MB.';
  return null;
}

export function formatFileSize(bytes: number | null): string {
  if (bytes === null) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** Nazwa pozycji na liście dokumentów wywiedziona z nazwy pliku (bez rozszerzenia). */
export function documentNameFromFile(fileName: string): string {
  const dot = fileName.lastIndexOf('.');
  return dot <= 0 ? fileName : fileName.slice(0, dot);
}

export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
