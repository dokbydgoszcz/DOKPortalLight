import { DokPath, DokStage } from './dok-case.model';

/** Etapy formacji dostępne na każdej ścieżce (ta sama tabela co DokStages w backendzie). Ostatni etap to Absolwent. */
const STAGES_BY_PATH: Record<DokPath, readonly DokStage[]> = {
  BaptismCandidate: ['Prekatechumenate', 'Catechumenate', 'Election', 'Neophyte', 'Graduate'],
  Confirmation: ['Evangelization', 'Graduate'],
  Communion: ['Evangelization', 'CloserFormation', 'Graduate'],
  Conversion: ['Evangelization', 'CloserFormation', 'Graduate'],
  ReturnToUnity: ['Evangelization', 'CloserFormation', 'Graduate']
};

export const stagesOf = (path: DokPath): readonly DokStage[] => STAGES_BY_PATH[path];

export const firstStageOf = (path: DokPath): DokStage => STAGES_BY_PATH[path][0];

/** Wszystkie etapy w kolejności wyświetlania (np. na pulpicie). */
export const ALL_STAGES: readonly DokStage[] = [
  'Evangelization', 'CloserFormation', 'Prekatechumenate', 'Catechumenate', 'Election', 'Neophyte', 'Graduate'
];

export const DOK_STAGE_LABELS: Record<DokStage, string> = {
  Evangelization: 'Ewangelizacja',
  CloserFormation: 'Formacja bliższa',
  Prekatechumenate: 'Prekatechumenat',
  Catechumenate: 'Katechumenat',
  Election: 'Wybranie',
  Neophyte: 'Neofita',
  Graduate: 'Absolwent'
};

export const DOK_PATH_LABELS: Record<DokPath, string> = {
  BaptismCandidate: 'Kandydaci do Chrztu',
  Confirmation: 'Bierzmowanie',
  Communion: 'Eucharystia',
  Conversion: 'Konwersja',
  ReturnToUnity: 'Powrót do Jedności'
};
