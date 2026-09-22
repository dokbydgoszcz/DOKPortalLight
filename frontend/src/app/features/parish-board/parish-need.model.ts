export interface ParishNeed {
  id: string;
  parishId: string;
  parishName: string;
  description: string;
  status: string;
  assignedPersonId: string | null;
  assignedPersonName: string | null;
  assignedAtUtc: string | null;
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
