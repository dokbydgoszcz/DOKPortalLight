import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, expect, it } from 'vitest';
import { SkspClassesService } from './sksp-classes.service';
import { api } from '../../testing/test-helpers';

describe('SkspClassesService', () => {
  it('lists all classes or one year', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(SkspClassesService);
    const http = TestBed.inject(HttpTestingController);

    service.list().subscribe();
    expect(http.expectOne(api('/api/sksp-classes')).request.params.has('year')).toBe(false);
    service.list(2).subscribe();
    expect(http.expectOne(r => r.url === api('/api/sksp-classes')).request.params.get('year')).toBe('2');
  });
});
