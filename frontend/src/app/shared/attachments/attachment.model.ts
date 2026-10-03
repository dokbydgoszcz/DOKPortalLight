export interface Attachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
}

export interface UploadSummary {
  uploaded: number;
  errors: string[];
}
