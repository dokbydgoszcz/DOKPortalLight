export interface CaseDocument {
  id: string;
  dokCaseId: string;
  name: string;
  isProvided: boolean;
  originalFileName: string | null;
  fileSizeBytes: number | null;
  uploadedAtUtc: string | null;
  hasFile: boolean;
}
