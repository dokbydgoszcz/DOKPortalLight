import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, expect, it } from 'vitest';
import { DocumentsService } from './documents.service';
import { api } from '../../testing/test-helpers';

function setupService() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { service: TestBed.inject(DocumentsService), http: TestBed.inject(HttpTestingController) };
}

describe('DocumentsService', () => {
  it('talks to the documents and templates endpoints', () => {
    const { service, http } = setupService();
    const t = api('/api/document-templates');
    service.history().subscribe();
    http.expectOne(api('/api/documents')).flush([]);
    service.download('d1', 'docx').subscribe();
    http.expectOne(r => r.url === api('/api/documents/d1/download') && r.params.get('format') === 'docx').flush(new Blob());
    service.download('d1').subscribe();
    http.expectOne(r => r.url === api('/api/documents/d1/download') && r.params.get('format') === 'pdf').flush(new Blob());
    service.delete('d1').subscribe();
    expect(http.expectOne(api('/api/documents/d1')).request.method).toBe('DELETE');
    service.generate({ letterTemplateId: 't1', personId: 'p1', inputs: {}, format: 'pdf' }).subscribe();
    expect(http.expectOne(api('/api/documents/generate')).request.body.letterTemplateId).toBe('t1');

    service.templates().subscribe();
    expect(http.expectOne(r => r.url === t).request.params.has('all')).toBe(false);
    service.templates(true).subscribe();
    http.expectOne(r => r.url === t && r.params.get('all') === 'true').flush([]);
    service.fields().subscribe();
    http.expectOne(`${t}/fields`).flush([]);
    service.createTemplate({ name: 'a', body: 'b', useLetterhead: true, isActive: true }).subscribe();
    expect(http.expectOne(r => r.url === t && r.method === 'POST').request.body.name).toBe('a');
    service.updateTemplate('t1', { name: 'a', body: 'b', useLetterhead: true, isActive: true }).subscribe();
    expect(http.expectOne(`${t}/t1`).request.method).toBe('PUT');
    service.deleteTemplate('t1').subscribe();
    expect(http.expectOne(`${t}/t1`).request.method).toBe('DELETE');
    service.preview('<p>x</p>', true, null).subscribe();
    expect(http.expectOne(`${t}/preview`).request.body).toEqual({ body: '<p>x</p>', useLetterhead: true, personId: null, format: 'pdf', inputs: undefined });
    service.letterhead().subscribe();
    http.expectOne(r => r.url === `${t}/letterhead` && r.method === 'GET').flush({});
    service.saveLetterhead({ headerText: 'a', footerText: 'b', place: 'c' }).subscribe();
    expect(http.expectOne(r => r.url === `${t}/letterhead` && r.method === 'PUT').request.body.place).toBe('c');
    service.logo().subscribe();
    http.expectOne(r => r.url === `${t}/letterhead/logo` && r.method === 'GET').flush(new Blob());
    service.uploadLogo(new File(['x'], 'logo.png')).subscribe();
    expect(http.expectOne(r => r.url === `${t}/letterhead/logo` && r.method === 'PUT').request.body instanceof FormData).toBe(true);
    service.deleteLogo().subscribe();
    expect(http.expectOne(r => r.url === `${t}/letterhead/logo` && r.method === 'DELETE')).toBeTruthy();
  });
});
