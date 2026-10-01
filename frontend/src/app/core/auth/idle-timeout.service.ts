import { Injectable, NgZone } from '@angular/core';
import { AuthService } from './auth.service';

const IDLE_LIMIT_MS = 20 * 60 * 1000;
const ACTIVITY_EVENTS = ['mousedown', 'mousemove', 'keydown', 'scroll', 'touchstart'] as const;

@Injectable({ providedIn: 'root' })
export class IdleTimeoutService {
  private timer: ReturnType<typeof setTimeout> | null = null;
  private readonly onActivity = () => this.resetTimer();

  constructor(
    private readonly auth: AuthService,
    private readonly zone: NgZone
  ) {}

  start(): void {
    this.zone.runOutsideAngular(() => {
      for (const eventName of ACTIVITY_EVENTS) {
        document.addEventListener(eventName, this.onActivity, { passive: true });
      }
    });
    this.resetTimer();
  }

  stop(): void {
    for (const eventName of ACTIVITY_EVENTS) {
      document.removeEventListener(eventName, this.onActivity);
    }
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  private resetTimer(): void {
    if (this.timer) {
      clearTimeout(this.timer);
    }
    this.timer = setTimeout(() => {
      this.zone.run(() => this.auth.logout('idle'));
    }, IDLE_LIMIT_MS);
  }
}
