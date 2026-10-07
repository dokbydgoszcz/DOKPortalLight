import { describe, expect, it } from 'vitest';
import { DOCUMENT_TEMPLATE_LABELS, OFFERED_TEMPLATES } from './generated-document.model';

// Ta sama lista co w backendzie (DocumentTemplates); zmiana w jednym miejscu wymaga zmiany w drugim.
describe('document templates', () => {
  it('offers the current templates in menu order', () => {
    expect(OFFERED_TEMPLATES.map(t => DOCUMENT_TEMPLATE_LABELS[t])).toEqual([
      'Pismo do Biskupa',
      'Zgoda na konwersję',
      'Wniosek o misję kanoniczną',
      'Skierowanie do DOK',
      'Zaświadczenie ukończenia SKŚP',
      'Zaświadczenie o ukończeniu studium katechumenalnego',
      'Zaświadczenie o chrzcie',
      'Zaświadczenie o bierzmowaniu',
      'Zaświadczenie o Eucharystii',
      'Zaświadczenie – ojciec chrzestny / matka chrzestna',
      'Klauzula RODO'
    ]);
  });

  it('keeps the old generic sacrament certificate only as a label for the history', () => {
    expect(OFFERED_TEMPLATES).not.toContain('SacramentCertificate');
    expect(DOCUMENT_TEMPLATE_LABELS['SacramentCertificate']).toBe('Zaświadczenie o sakramencie (dawne)');
  });

  it('calls the mission document an application, not a decree', () => {
    expect(Object.values(DOCUMENT_TEMPLATE_LABELS)).not.toContain('Dekret misji kanonicznej');
    expect(DOCUMENT_TEMPLATE_LABELS['CanonicalMissionApplication']).toBe('Wniosek o misję kanoniczną');
  });
});
