import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIcon } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state', standalone: true, imports: [MatIcon],
  template: `<section class="empty-state"><mat-icon aria-hidden="true">inbox</mat-icon><h2>{{ title() }}</h2><p>{{ message() }}</p><ng-content /></section>`,
  styles: [`.empty-state{text-align:center;padding:3rem 1.5rem;color:var(--claims-text-muted)}mat-icon{width:3rem;height:3rem;font-size:3rem;color:var(--claims-primary)}h2{margin:.75rem 0 .25rem;color:var(--claims-text)}p{margin:.25rem auto 1rem;max-width:34rem}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EmptyStateComponent {
  readonly title = input('Nothing here yet');
  readonly message = input('Content will appear here when it becomes available.');
}
