import { HttpResponse } from '@angular/common/http';

/** Dawne wbudowane typy pism (stare wpisy historii i wzory startowe). */
export type DocumentTemplate =
  | 'LetterToBishop'
  | 'ConversionConsent'
  | 'CanonicalMissionApplication'
  | 'DokReferral'
  | 'SkspCompletionCertificate'
  | 'SacramentCertificate'
  | 'BaptismCertificate'
  | 'ConfirmationCertificate'
  | 'EucharistCertificate'
  | 'CatechumenateStudyCertificate'
  | 'GodparentCertificate'
  | 'GdprClause';

export interface GeneratedDocument {
  id: string;
  template: DocumentTemplate | null;
  letterTemplateId: string | null;
  /** Nazwa pisma (wzoru w chwili generowania). */
  title: string;
  canDownloadWord: boolean;
  personId: string;
  personFullName: string;
  generatedByUserId: string;
  additionalNotes: string | null;
  createdAtUtc: string;
  hasStoredFile: boolean;
  downloadCount: number;
}

export type LetterFormat = 'pdf' | 'docx';

export interface GenerateDocumentValue {
  letterTemplateId: string;
  personId: string;
  additionalNotes?: string;
  inputs: Record<string, string>;
  format: LetterFormat;
}

export interface LetterTemplate {
  id: string;
  name: string;
  body: string;
  useLetterhead: boolean;
  isActive: boolean;
  sortOrder: number;
  updatedAtUtc: string;
  updatedByEmail: string | null;
  /** Pola {{wpisz:…}} – formularz generowania zapyta o nie. */
  inputs: string[];
  unknownFields: string[];
  usageCount: number;
}

export interface SaveLetterTemplate {
  name: string;
  body: string;
  useLetterhead: boolean;
  isActive: boolean;
}

export interface LetterField {
  key: string;
  label: string;
  group: string;
  example: string;
}

export interface Letterhead {
  headerText: string | null;
  footerText: string | null;
  place: string | null;
  hasLogo: boolean;
  updatedAtUtc: string | null;
}

/** Pole do wpisania przy generowaniu: {{wpisz:Data chrztu}}. */
export const INPUT_PREFIX = 'wpisz:';

const TOKEN = /\{\{\s*([^{}]+?)\s*\}\}/g;

function plainText(html: string): string {
  return html.replace(/<[^>]+>/g, '').replace(/&amp;/g, '&').replace(/&nbsp;/g, ' ');
}

/** Pola {{wpisz:…}} w treści, bez powtórzeń. */
export function inputsOf(body: string): string[] {
  const seen = new Map<string, string>();
  for (const match of plainText(body).matchAll(TOKEN)) {
    const key = match[1].trim();
    if (!key.toLowerCase().startsWith(INPUT_PREFIX)) continue;
    const name = key.slice(INPUT_PREFIX.length).trim();
    if (name && !seen.has(name.toLowerCase())) seen.set(name.toLowerCase(), name);
  }
  return [...seen.values()];
}

/** Pola, których portal nie zna (np. literówka w {{osoba.imie}}). */
export function unknownFieldsOf(body: string, fields: LetterField[]): string[] {
  const known = new Set(fields.map(f => f.key.toLowerCase()));
  const unknown = new Set<string>();
  for (const match of plainText(body).matchAll(TOKEN)) {
    const key = match[1].trim();
    if (!key.toLowerCase().startsWith(INPUT_PREFIX) && !known.has(key.toLowerCase())) unknown.add(key);
  }
  return [...unknown];
}

export interface DownloadedFile {
  blob: Blob;
  fileName: string;
  /** Pola bez danych – w piśmie zostały kropki do wypełnienia. */
  missing: string[];
}

/** Nazwa pliku z nagłówka Content-Disposition i brakujące pola z X-Missing-Fields. */
export function downloadedFile(response: HttpResponse<Blob>, fallback: string): DownloadedFile {
  const disposition = response.headers.get('Content-Disposition') ?? '';
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  const fileName = star ? decodeURIComponent(star[1]) : plain ? plain[1] : fallback;
  const missingHeader = response.headers.get('X-Missing-Fields');
  const missing = missingHeader ? decodeURIComponent(missingHeader).split('|').filter(Boolean) : [];
  return { blob: response.body ?? new Blob(), fileName, missing };
}
