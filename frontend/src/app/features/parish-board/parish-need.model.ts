export interface AssignedPerson {
  personId: string;
  fullName: string;
  assignedAtUtc: string;
}

export interface ParishNeed {
  id: string;
  parishId: string;
  parishName: string;
  description: string;
  status: string;
  assignedPeople: AssignedPerson[];
}

export interface CreateParishNeedValue {
  parishId: string;
  description: string;
}

export interface Parish {
  id: string;
  name: string;
  city: string | null;
  /** Proboszcz: osoba z funkcją Proboszcz przypisaną do tej parafii. */
  pastorPersonId?: string | null;
  pastorFullName?: string | null;
  deanery?: string | null;
  address?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
}

/** Wynik importu spisu parafii z proboszczami (podgląd albo zapis). */
export interface ParishImportResult {
  applied: boolean;
  newParishes: number;
  matchedParishes: number;
  newPeople: number;
  existingPeople: number;
  pastorsSet: number;
  pastorsUnchanged: number;
  pastorsReplaced: number;
  rows: ParishImportRow[];
}

export interface ParishImportRow {
  row: number;
  deanery: string | null;
  city: string;
  parishName: string;
  newParish: boolean;
  pastorFullName: string;
  newPerson: boolean;
  pastorChange: 'set' | 'unchanged' | 'replaced';
  previousPastor: string | null;
  warnings: string[];
}
