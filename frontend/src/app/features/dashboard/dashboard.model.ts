export interface DokStageCount {
  stage: 'Application' | 'Formation' | 'Sacrament' | 'Graduate';
  count: number;
}

export interface DashboardSummary {
  peopleCount: number;
  parishCount: number;
  dokCasesByStage: DokStageCount[];
  missingDocumentsCasesCount: number;
  upcomingMeetingsCount: number;
  activeCandidatesCount: number;
}
