export type FormSubmissionStatus = 'Pending' | 'Applied' | 'Rejected';

export interface FormSubmissionFile {
  name: string;
  url: string;
  mimeType: string | null;
  size: number | null;
}

export interface FormSubmissionField {
  label: string;
  type: string | null;
  text: string | null;
  files: FormSubmissionFile[] | null;
}

/** Dane osoby odczytane i „posprzątane” z odpowiedzi. */
export interface PersonDraft {
  firstName: string | null;
  lastName: string | null;
  email: string | null;
  phone: string | null;
  addressStreet: string | null;
  addressPostalCode: string | null;
  addressCity: string | null;
  nameDayDay: number | null;
  nameDayMonth: number | null;
  photoIndex: number | null;
}

export interface FormSubmissionCandidate extends Omit<PersonDraft, 'photoIndex'> {
  personId: string;
  fullName: string;
  matchedBy: string;
  hasPhoto: boolean;
  functions: string[];
}

export interface FormSubmission {
  id: string;
  source: string;
  formName: string | null;
  submittedAtUtc: string;
  status: FormSubmissionStatus;
  fields: FormSubmissionField[];
  files: FormSubmissionFile[];
  draft: PersonDraft;
  candidates: FormSubmissionCandidate[];
  personId: string | null;
  personFullName: string | null;
  reviewedByEmail: string | null;
  reviewedAtUtc: string | null;
}

export interface ApplyFormSubmission {
  personId: string | null;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  addressStreet: string;
  addressPostalCode: string;
  addressCity: string;
  nameDayDay: number | null;
  nameDayMonth: number | null;
}
