export interface Formator {
  id: string;
  personId: string;
  personFullName: string;
  personEmail: string | null;
  personPhone: string | null;
  function: string;
}

export interface FormatorFormValue {
  personId: string;
  function: string;
}
