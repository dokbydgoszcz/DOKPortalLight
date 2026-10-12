import { Attachment } from '../../shared/attachments/attachment.model';

export interface PastoralNote {
  id: string;
  dokCaseId: string;
  authorUserId: string;
  authorEmail: string | null;
  content: string;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  attachments: Attachment[];
}

/** Komu udostępniłem swoje notatki w sprawie i czy mogę je udostępnić obecnemu katechiście. */
export interface NoteSharing {
  casePersonFullName: string;
  hasOwnNotes: boolean;
  currentCatechistPersonId: string;
  currentCatechistFullName: string;
  canShareWithCurrentCatechist: boolean;
  sharedWith: { personId: string; fullName: string; sharedAtUtc: string }[];
}

/** Sprawa z moimi notatkami, którą prowadzi już inny katechista. */
export interface HandedOverCase {
  caseId: string;
  personFullName: string;
  currentCatechistPersonId: string;
  currentCatechistFullName: string;
  notesCount: number;
  sharedWithCurrentCatechist: boolean;
}
