import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { AttachmentsComponent } from '../../shared/attachments/attachments.component';
import { ToastService } from '../../core/notifications/toast.service';
import { Resource, ResourceFormValue } from './resource.model';
import { ResourcesService } from './resources.service';

/** Globalna biblioteka zasobów dla katechistów: wgrywa Superwizor i Dyrektorzy, przegląda każdy z uprawnieniem. */
@Component({
  selector: 'app-resources',
  standalone: true,
  imports: [FormsModule, HasPermissionDirective, AttachmentsComponent],
  templateUrl: './resources.component.html',
  styleUrl: './resources.component.scss'
})
export class ResourcesComponent implements OnInit {
  readonly resources = signal<Resource[]>([]);
  readonly query = signal('');
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  formValue: ResourceFormValue = { title: '' };

  constructor(
    private readonly resourcesService: ResourcesService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.resourcesService.list(this.query()).subscribe({
      next: resources => this.resources.set(resources),
      error: () => this.toast.error('Nie udało się wczytać zasobów.')
    });
  }

  onSearch(value: string): void {
    this.query.set(value);
    this.load();
  }

  attachmentsUrl(resource: Resource): string {
    return this.resourcesService.attachmentsUrl(resource.id);
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.formValue = { title: '' };
    this.isFormOpen.set(true);
  }

  openEditForm(resource: Resource): void {
    this.editingId.set(resource.id);
    this.formValue = { title: resource.title, description: resource.description ?? undefined };
    this.isFormOpen.set(true);
  }

  onSave(): void {
    const title = this.formValue.title.trim();
    if (!title) {
      this.toast.error('Podaj tytuł zasobu.');
      return;
    }
    const description = this.formValue.description?.trim();
    const value: ResourceFormValue = description ? { title, description } : { title };
    const id = this.editingId();
    const request$ = id ? this.resourcesService.update(id, value) : this.resourcesService.create(value);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano zasób. Dołącz do niego pliki.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się zapisać zasobu.')
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  deleteResource(resource: Resource): void {
    if (!confirm(`Usunąć zasób „${resource.title}” razem z plikami?`)) return;
    this.resourcesService.delete(resource.id).subscribe({
      next: () => {
        this.toast.success('Zasób usunięty.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć zasobu.')
    });
  }
}
