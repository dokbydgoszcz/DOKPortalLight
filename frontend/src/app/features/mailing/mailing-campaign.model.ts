import { Attachment } from '../../shared/attachments/attachment.model';

export type MailingGroup = 'CandidatesSksp' | 'Missionaries' | 'DokGraduates' | 'DokCases' | 'Pastors' | 'Formators' | 'Custom';
/** „Sending” – kampania w kolejce, wiadomości wychodzą w tle partiami (limit dobowy skrzynki). */
export type CampaignStatus = 'Draft' | 'Sent' | 'Sending';

export interface MailingCampaign {
  id: string;
  subject: string;
  body: string;
  group: MailingGroup;
  recipientCount: number;
  /** Ilu adresatom nie udało się wysłać wiadomości (po wysyłce). */
  failedCount?: number;
  /** Wiadomości czekające w kolejce (kampania w trakcie wysyłki). */
  pendingCount?: number;
  status: CampaignStatus;
  createdAtUtc: string;
  sentAtUtc: string | null;
  /** Osoby własnej listy (grupa „Custom”). */
  personIds?: string[];
  attachments?: Attachment[];
}

export interface CreateMailingCampaignValue {
  subject: string;
  body: string;
  group: MailingGroup;
  personIds?: string[];
}

/** Adresat w podglądzie; bez e-maila wiadomości nie dostanie. */
export interface MailingRecipient {
  personId: string;
  fullName: string;
  email: string | null;
  /** Osoba wypisała się z mailingu – wiadomości nie dostanie. */
  optedOut?: boolean;
}

export const MAILING_GROUP_LABELS: Record<MailingGroup, string> = {
  CandidatesSksp: 'Kandydaci SKŚP',
  Missionaries: 'Katechiści',
  DokGraduates: 'Absolwenci DOK',
  DokCases: 'Podopieczni DOK',
  Pastors: 'Proboszczowie',
  Formators: 'Formatorzy',
  Custom: 'Własna lista osób'
};
