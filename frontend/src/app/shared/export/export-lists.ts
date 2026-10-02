import { Permissions } from '../../core/auth/permissions';

export type ExportListKey =
  | 'people' | 'dok-cases' | 'candidates' | 'missions' | 'formators' | 'supervisions' | 'meetings' | 'parishes';

export interface ExportListConfig {
  path: string;
  fileName: string;
  permission: string;
}

export const EXPORT_LISTS: Record<ExportListKey, ExportListConfig> = {
  people: { path: 'people', fileName: 'osoby.xlsx', permission: Permissions.PeopleExport },
  'dok-cases': { path: 'dok-cases', fileName: 'podopieczni-dok.xlsx', permission: Permissions.DokCasesExport },
  candidates: { path: 'candidates', fileName: 'kandydaci-sksp.xlsx', permission: Permissions.CandidatesExport },
  missions: { path: 'missions', fileName: 'katechisci-poslani.xlsx', permission: Permissions.MissionsExport },
  formators: { path: 'formators', fileName: 'formatorzy.xlsx', permission: Permissions.FormatorsExport },
  supervisions: { path: 'supervisions', fileName: 'superwizje.xlsx', permission: Permissions.SupervisionsExport },
  meetings: { path: 'meetings', fileName: 'spotkania.xlsx', permission: Permissions.MeetingsExport },
  parishes: { path: 'parishes', fileName: 'parafie.xlsx', permission: Permissions.ParishesExport }
};
