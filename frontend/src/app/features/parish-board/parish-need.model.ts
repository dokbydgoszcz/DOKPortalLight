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
}
