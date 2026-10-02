export type ExportListKey =
  | 'people' | 'dok-cases' | 'candidates' | 'missions' | 'formators' | 'supervisions' | 'meetings' | 'parishes';

export interface ExportListConfig {
  path: string;
  fileName: string;
  roles: string[];
}

export const EXPORT_LISTS: Record<ExportListKey, ExportListConfig> = {
  people: { path: 'people', fileName: 'osoby.xlsx', roles: ['Administrator', 'DyrektorSKSP', 'DyrektorDOK'] },
  'dok-cases': { path: 'dok-cases', fileName: 'podopieczni-dok.xlsx', roles: ['Administrator', 'DyrektorDOK'] },
  candidates: { path: 'candidates', fileName: 'kandydaci-sksp.xlsx', roles: ['Administrator', 'DyrektorSKSP'] },
  missions: { path: 'missions', fileName: 'katechisci-poslani.xlsx', roles: ['Administrator', 'DyrektorSKSP'] },
  formators: { path: 'formators', fileName: 'formatorzy.xlsx', roles: ['Administrator', 'DyrektorSKSP'] },
  supervisions: { path: 'supervisions', fileName: 'superwizje.xlsx', roles: ['Administrator', 'DyrektorDOK', 'DyrektorSKSP', 'Superwizor'] },
  meetings: { path: 'meetings', fileName: 'spotkania.xlsx', roles: ['Administrator', 'DyrektorDOK'] },
  parishes: { path: 'parishes', fileName: 'parafie.xlsx', roles: ['Administrator'] }
};
