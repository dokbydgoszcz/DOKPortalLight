import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastContainerComponent } from './core/notifications/toast-container.component';
import { ConfirmDialogComponent } from './core/confirm/confirm-dialog.component';

@Component({
  imports: [RouterOutlet, ToastContainerComponent, ConfirmDialogComponent],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('frontend');
}
