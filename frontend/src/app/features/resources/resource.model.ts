import { Attachment } from '../../shared/attachments/attachment.model';

export interface Resource {
  id: string;
  title: string;
  description: string | null;
  createdAtUtc: string;
  files: Attachment[];
}

export interface ResourceFormValue {
  title: string;
  description?: string;
}
