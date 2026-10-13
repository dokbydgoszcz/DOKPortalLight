import { Person } from './person.model';

export interface OverviewCase {
  id: string;
  personFullName: string;
  path: string;
  stage: string;
  catechistFullName: string;
  role: 'podopieczny' | 'katechista' | 'mentor';
  interruptedOn: string | null;
  completedAtUtc: string | null;
}

/** Karta osoby; sekcja null = brak dostępu do jej ekranu. */
export interface PersonOverview {
  person: Person;
  dokCases: OverviewCase[] | null;
  casesLed: OverviewCase[] | null;
  candidate: { id: string; formationYear: number; status: string; attendancePercentage: number | null; opinionsCollected: number; opinionsRequired: number } | null;
  candidateVisible: boolean;
  missions: { id: string; servicePlace: string; missionStartDate: string; missionEndDate: string; supervisionGroup: string | null; status: string }[] | null;
  parishAssignments: { parishName: string; needDescription: string; assignedAtUtc: string }[] | null;
  formatorFunctions: string[] | null;
  documents: { id: string; title: string; createdAtUtc: string }[] | null;
  supervisions: { present: number; listed: number; lastPresentOn: string | null } | null;
  account: { email: string; roles: string[]; isActive: boolean } | null;
  accountVisible: boolean;
}
