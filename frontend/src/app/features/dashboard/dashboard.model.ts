import { DokPath, DokStage } from '../dok-cases/dok-case.model';

export interface DokStageCount {
  stage: DokStage;
  count: number;
}

/** Podopieczny, który jest na jednym etapie dłużej niż rok. */
export interface StalledCase {
  caseId: string;
  personFullName: string;
  path: DokPath;
  stage: DokStage;
  stageSinceUtc: string;
  monthsOnStage: number;
}

export interface UpcomingMeeting {
  meetingId: string;
  meetingDate: string;
  /** Podopieczny (spotkanie indywidualne) albo nazwa grupy. */
  label: string;
}

export interface MissingDocumentsCase {
  caseId: string;
  personFullName: string;
  path: DokPath;
  missingDocuments: string[];
}

export interface DashboardSummary {
  peopleCount: number;
  parishCount: number;
  dokCasesByStage: DokStageCount[];
  missingDocumentsCasesCount: number;
  upcomingMeetingsCount: number;
  upcomingMeetings: UpcomingMeeting[];
  missingDocumentsCases: MissingDocumentsCase[];
  activeCandidatesCount: number;
  stalledCases: StalledCase[];
  stalledCasesCount: number;
}
