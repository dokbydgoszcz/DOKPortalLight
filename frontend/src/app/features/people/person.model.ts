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
}
