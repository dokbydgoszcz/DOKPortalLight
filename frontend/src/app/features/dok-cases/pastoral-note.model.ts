import { Attachment } from '../../shared/attachments/attachment.model';

export interface PastoralNote {
  id: string;
  dokCaseId: string;
  authorUserId: string;
  authorEmail: string | null;
  content: string;
  createdAtUtc: string;
  attachments: Attachment[];
}
