export interface PastoralNote {
  id: string;
  dokCaseId: string;
  authorUserId: string;
  authorEmail: string | null;
  content: string;
  createdAtUtc: string;
}
