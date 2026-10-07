export type FunctionType = 'Catechist' | 'Acolyte' | 'Lector' | 'Pastor';

export const FUNCTION_LABELS: Record<FunctionType, string> = {
  Catechist: 'Katechista',
  Acolyte: 'Akolita',
  Lector: 'Lektor',
  Pastor: 'Proboszcz'
};

export const FUNCTION_TYPES = Object.keys(FUNCTION_LABELS) as FunctionType[];

export interface PersonFunction {
  id: string;
  type: FunctionType;
  parishId: string | null;
  parishName: string | null;
  institutedOn: string | null;
  notes: string | null;
}

/** „Proboszcz (św. Jana)” – parafia tylko, gdy funkcja ją ma. */
export function functionText(f: Pick<PersonFunction, 'type' | 'parishName'>): string {
  return f.parishName ? `${FUNCTION_LABELS[f.type]} (${f.parishName})` : FUNCTION_LABELS[f.type];
}

export interface PersonFunctionInput {
  type: FunctionType;
  parishId?: string;
  institutedOn?: string;
  notes?: string;
}

export interface Person {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string | null;
  phone: string | null;
  birthDate: string | null;
  parishId: string | null;
  parishName: string | null;
  notes: string | null;
  nameDayMonth: number | null;
  nameDayDay: number | null;
  functions: PersonFunction[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PersonFormValue {
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  birthDate?: string;
  parishId?: string;
  notes?: string;
  nameDayMonth?: number;
  nameDayDay?: number;
  functions?: PersonFunctionInput[];
  confirmDuplicate?: boolean;
}
