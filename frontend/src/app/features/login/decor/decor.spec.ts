import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { GlowFrameComponent } from './glow-frame.component';
import { LoginRibbonsComponent } from './login-ribbons.component';
import { RIBBON_PERIOD, ribbonPath } from './ribbon-path';

const shape = { center: 300, amplitude: 60, thickness: 30, twist: 20, phase: 0.5 };

function points(d: string): number[][] {
  return d.replace(/^M|Z$/g, '').split('L').map(p => p.split(',').map(Number));
}

describe('ribbonPath', () => {
  it('draws a closed band two wave periods wide, so sliding it by one period loops without a seam', () => {
    const pts = points(ribbonPath(shape, 96));
    expect(pts.length).toBe(2 * 97);
    const top = pts.slice(0, 97);
    expect(top[0][0]).toBe(0);
    expect(top[96][0]).toBe(RIBBON_PERIOD * 2);
    // Ten sam kształt na początku pierwszego i drugiego okresu.
    expect(top[48][1]).toBeCloseTo(top[0][1], 1);
    // Dolna krawędź zawsze pod górną, a grubość nigdy nie spada do zera.
    const bottom = pts.slice(97).reverse();
    bottom.forEach((p, i) => expect(p[1] - top[i][1]).toBeGreaterThanOrEqual(shape.thickness - shape.twist - 0.2));
  });
});

@Component({
  standalone: true,
  imports: [GlowFrameComponent],
  template: `<app-glow-frame><div class="content">Treść</div></app-glow-frame>`
})
class FrameHost {}

describe('login decorations', () => {
  it('draws decorative ribbons hidden from screen readers', () => {
    TestBed.configureTestingModule({ imports: [LoginRibbonsComponent] });
    const fixture = TestBed.createComponent(LoginRibbonsComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('svg')!.getAttribute('aria-hidden')).toBe('true');
    const paths = el.querySelectorAll('path.ribbon');
    expect(paths.length).toBe(3);
    expect(paths[0].getAttribute('d')).toMatch(/^M.*Z$/);
    expect(paths[0].getAttribute('fill')).toBe('url(#ribbon-gold)');
  });

  it('frames its content with a light that screen readers ignore', () => {
    TestBed.configureTestingModule({ imports: [FrameHost] });
    const fixture = TestBed.createComponent(FrameHost);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-glow-frame .content')!.textContent).toBe('Treść');
    expect(el.querySelectorAll('app-glow-frame [aria-hidden="true"]').length).toBe(2);
  });
});
