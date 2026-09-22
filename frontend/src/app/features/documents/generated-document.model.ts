export type DocumentTemplate =
  | 'LetterToBishop'
  | 'ConversionConsent'
  | 'CanonicalMissionDecree'
  | 'DokReferral'
  | 'SkspCompletionCertificate'
  | 'SacramentCertificate';

export interface GeneratedDocument {
  id: string;
  template: DocumentTemplate;
  personId: string;
  personFullName: string;
  generatedByUserId: string;
  additionalNotes: string | null;
  createdAtUtc: string;
}

export interface GenerateDocumentValue {
  template: DocumentTemplate;
  personId: string;
  additionalNotes?: string;
}

export const DOCUMENT_TEMPLATE_LABELS: Record<DocumentTemplate, string> = {
  LetterToBishop: 'Pismo do Biskupa',
  ConversionConsent: 'Zgoda na konwersję',
  CanonicalMissionDecree: 'Dekret misji kanonicznej',
  DokReferral: 'Skierowanie do DOK',
  SkspCompletionCertificate: 'Zaświadczenie ukończenia SKŚP',
  SacramentCertificate: 'Zaświadczenie o sakramencie'
};
