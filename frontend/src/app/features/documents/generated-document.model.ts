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
  template: DocumentTemplate;
  personId: string;
  personFullName: string;
  generatedByUserId: string;
  additionalNotes: string | null;
  createdAtUtc: string;
  hasStoredFile: boolean;
  downloadCount: number;
}

export interface GenerateDocumentValue {
  template: DocumentTemplate;
  personId: string;
  additionalNotes?: string;
}

export const DOCUMENT_TEMPLATE_LABELS: Record<DocumentTemplate, string> = {
  LetterToBishop: 'Pismo do Biskupa',
  ConversionConsent: 'Zgoda na konwersję',
  CanonicalMissionApplication: 'Wniosek o misję kanoniczną',
  DokReferral: 'Skierowanie do DOK',
  SkspCompletionCertificate: 'Zaświadczenie ukończenia SKŚP',
  SacramentCertificate: 'Zaświadczenie o sakramencie (dawne)',
  BaptismCertificate: 'Zaświadczenie o chrzcie',
  ConfirmationCertificate: 'Zaświadczenie o bierzmowaniu',
  EucharistCertificate: 'Zaświadczenie o Eucharystii',
  CatechumenateStudyCertificate: 'Zaświadczenie o ukończeniu studium katechumenalnego',
  GodparentCertificate: 'Zaświadczenie – ojciec chrzestny / matka chrzestna',
  GdprClause: 'Klauzula RODO'
};

/** Typy pism, które można dziś wygenerować, w kolejności menu (dawne typy zostają tylko w historii). */
export const OFFERED_TEMPLATES: readonly DocumentTemplate[] = [
  'LetterToBishop',
  'ConversionConsent',
  'CanonicalMissionApplication',
  'DokReferral',
  'SkspCompletionCertificate',
  'CatechumenateStudyCertificate',
  'BaptismCertificate',
  'ConfirmationCertificate',
  'EucharistCertificate',
  'GodparentCertificate',
  'GdprClause'
];
