import { describe, expect, it } from 'vitest';
import { ALL_STAGES, DOK_PATH_LABELS, DOK_STAGE_LABELS, firstStageOf, stagesOf } from './dok-stages';
import { DokPath } from './dok-case.model';

const PATHS: DokPath[] = ['BaptismCandidate', 'Confirmation', 'Communion', 'Conversion', 'ReturnToUnity'];

// Ta sama tabela co w backendzie (DokStages); zmiana w jednym miejscu wymaga zmiany w drugim.
describe('DOK stages per path', () => {
  it('lists five stages for baptism, ending with the graduate', () => {
    expect(stagesOf('BaptismCandidate')).toEqual(['Prekatechumenate', 'Catechumenate', 'Election', 'Neophyte', 'Graduate']);
  });

  it('gives confirmation evangelization and graduate', () => {
    expect(stagesOf('Confirmation')).toEqual(['Evangelization', 'Graduate']);
  });

  it.each(['Communion', 'Conversion', 'ReturnToUnity'] as DokPath[])('gives %s evangelization, closer formation and graduate', path => {
    expect(stagesOf(path)).toEqual(['Evangelization', 'CloserFormation', 'Graduate']);
  });

  it('starts every path at its first stage and ends it with Absolwent', () => {
    for (const path of PATHS) {
      expect(firstStageOf(path)).toBe(stagesOf(path)[0]);
      expect(stagesOf(path).at(-1)).toBe('Graduate');
    }
    expect(firstStageOf('BaptismCandidate')).toBe('Prekatechumenate');
    expect(firstStageOf('Confirmation')).toBe('Evangelization');
  });

  it('has a Polish label for every stage and path, with Eucharystia instead of Stół Pański', () => {
    expect(DOK_STAGE_LABELS).toEqual({
      Evangelization: 'Ewangelizacja',
      CloserFormation: 'Formacja bliższa',
      Prekatechumenate: 'Prekatechumenat',
      Catechumenate: 'Katechumenat',
      Election: 'Wybranie',
      Neophyte: 'Neofita',
      Graduate: 'Absolwent'
    });
    expect(DOK_PATH_LABELS['Communion']).toBe('Eucharystia');
    expect(DOK_PATH_LABELS['Confirmation']).toBe('Bierzmowanie');
    expect(Object.keys(DOK_PATH_LABELS)).toEqual(PATHS);
  });

  it('lists all stages once, in formation order', () => {
    expect(ALL_STAGES).toEqual(['Evangelization', 'CloserFormation', 'Prekatechumenate', 'Catechumenate', 'Election', 'Neophyte', 'Graduate']);
  });
});
