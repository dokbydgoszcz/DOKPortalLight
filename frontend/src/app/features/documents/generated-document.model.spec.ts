import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { downloadedFile, inputsOf, unknownFieldsOf } from './generated-document.model';

describe('letter template helpers', () => {
  it('lists the fields to type in, once each, ignoring markup', () => {
    expect(inputsOf('<p>{{wpisz: Data chrztu}} i <b>{{wpisz:data chrztu}}</b> oraz {{wpisz:Parafia}} {{osoba.imię}} {{wpisz: }}</p>'))
      .toEqual(['Data chrztu', 'Parafia']);
  });

  it('accepts cases for names and gender forms, and flags the wrong ones', () => {
    const fields = [
      { key: 'osoba.imię', label: 'Imię', group: 'Osoba', example: 'Jan', declinable: true },
      { key: 'pismo.data', label: 'Data', group: 'Pismo', example: '16.10.2026' }
    ];
    expect(unknownFieldsOf('<p>{{osoba.imię|dopełniacz}} {{osoba.imię|Celownik}} {{rodzaj:Pan|Pani}} {{osoba.imię|wolacz2}} {{pismo.data|dopełniacz}} {{rodzaj:Pan}}</p>', fields))
      .toEqual(['osoba.imię|wolacz2', 'pismo.data|dopełniacz', 'rodzaj:Pan']);
  });

  it('finds unknown fields', () => {
    const fields = [{ key: 'osoba.imię', label: 'Imię', group: 'Osoba', example: 'Jan' }];
    expect(unknownFieldsOf('<p>{{osoba.imię}} {{OSOBA.IMIĘ}} {{osoba.imie}} {{wpisz:Coś}} {{osoba.imie}}</p>', fields)).toEqual(['osoba.imie']);
  });

  it('reads the file name and the missing fields from the response', () => {
    const blob = new Blob(['x']);
    const utf = downloadedFile(new HttpResponse({
      body: blob,
      headers: new HttpHeaders({ 'Content-Disposition': "attachment; filename=a.pdf; filename*=UTF-8''Za%C5%9Bwiadczenie.pdf", 'X-Missing-Fields': encodeURIComponent('Data chrztu|Misja od') })
    }), 'x.pdf');
    expect(utf).toEqual({ blob, fileName: 'Zaświadczenie.pdf', missing: ['Data chrztu', 'Misja od'] });
    expect(downloadedFile(new HttpResponse({ body: blob, headers: new HttpHeaders({ 'Content-Disposition': 'attachment; filename="b.docx"' }) }), 'x').fileName).toBe('b.docx');
    const bare = downloadedFile(new HttpResponse<Blob>({ body: null }), 'x.pdf');
    expect(bare.missing).toEqual([]);
    expect(bare.fileName).toBe('x.pdf');
  });
});
