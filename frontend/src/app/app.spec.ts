import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    })
      .compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('shows notifications on every screen, also on the login page', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('app-toast-container')).not.toBeNull();
  });
});
