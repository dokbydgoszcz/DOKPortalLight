export type MailingGroup = 'CandidatesSksp' | 'Missionaries' | 'DokGraduates' | 'DokCases';
export type CampaignStatus = 'Draft' | 'Sent';

export interface MailingCampaign {
  id: string;
  subject: string;
  body: string;
  group: MailingGroup;
  recipientCount: number;
  status: CampaignStatus;
  createdAtUtc: string;
  sentAtUtc: string | null;
}

export interface CreateMailingCampaignValue {
  subject: string;
  body: string;
  group: MailingGroup;
}

export const MAILING_GROUP_LABELS: Record<MailingGroup, string> = {
  CandidatesSksp: 'Kandydaci SKŚP',
  Missionaries: 'Katechiści posłani',
  DokGraduates: 'Absolwenci DOK',
  DokCases: 'Podopieczni DOK'
};
