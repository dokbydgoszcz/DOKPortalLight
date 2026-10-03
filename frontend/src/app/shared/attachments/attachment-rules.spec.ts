import { describe, expect, it } from 'vitest';
import { ACCEPT_ATTRIBUTE, ALLOWED_TYPES_LABEL, MAX_FILE_BYTES, documentNameFromFile, formatFileSize, validateFile } from './attachment-rules';

const file = (name: string, size = 10) => ({ name, size }) as File;

describe('validateFile', () => {
  it.each(['skan.pdf', 'zdjecie.JPG', 'zdjecie.jpeg', 'zrzut.png', 'pismo.docx', 'pismo.doc', 'notatka.TXT'])(
    'accepts %s',
    name => expect(validateFile(file(name))).toBeNull()
  );

  it.each(['program.exe', 'arkusz.xlsx', 'bez-rozszerzenia', 'archiwum.pdf.zip'])('rejects %s and names the allowed types', name => {
    const error = validateFile(file(name));

    expect(error).toContain('Niedozwolony typ pliku');
    expect(error).toContain(ALLOWED_TYPES_LABEL);
  });

  it('rejects an empty file', () => {
    expect(validateFile(file('a.pdf', 0))).toBe('Plik jest pusty.');
  });

  it('accepts exactly 20 MB and rejects one byte more', () => {
    expect(MAX_FILE_BYTES).toBe(20 * 1024 * 1024);
    expect(validateFile(file('a.pdf', MAX_FILE_BYTES))).toBeNull();
    expect(validateFile(file('a.pdf', MAX_FILE_BYTES + 1))).toContain('20 MB');
  });
});

describe('ACCEPT_ATTRIBUTE', () => {
  it('lists every allowed extension for the file picker', () => {
    expect(ACCEPT_ATTRIBUTE).toBe('.pdf,.jpg,.jpeg,.png,.docx,.doc,.txt');
  });
});

describe('formatFileSize', () => {
  it('formats B, KB and MB, and nothing for a missing size', () => {
    expect(formatFileSize(null)).toBe('');
    expect(formatFileSize(512)).toBe('512 B');
    expect(formatFileSize(2048)).toBe('2.0 KB');
    expect(formatFileSize(3 * 1024 * 1024)).toBe('3.0 MB');
  });
});

describe('documentNameFromFile', () => {
  it('drops the extension and tidies the name', () => {
    expect(documentNameFromFile('Metryka chrztu.pdf')).toBe('Metryka chrztu');
    expect(documentNameFromFile('akt_urodzenia-Jan.JPG')).toBe('akt_urodzenia-Jan');
    expect(documentNameFromFile('bez-rozszerzenia')).toBe('bez-rozszerzenia');
    expect(documentNameFromFile('.pdf')).toBe('.pdf');
  });
});
