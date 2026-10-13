import { Attachment } from '../../shared/attachments/attachment.model';

export type MailingGroup = 'CandidatesSksp' | 'Missionaries' | 'DokGraduates' | 'DokCases' | 'Pastors' | 'Formators' | 'Custom';
export type CampaignStatus = 'Draft' | 'Sent';

export interface MailingCampaign {
  id: string;
  subject: string;
  body: string;
  group: MailingGroup;
  recipientCount: number;
  /** Ilu adresatom nie udało się wysłać wiadomości (po wysyłce). */
  failedCount?: number;
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
