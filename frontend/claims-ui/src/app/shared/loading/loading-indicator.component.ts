import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatProgressSpinner } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-loading-indicator', standalone: true, imports: [MatProgressSpinner],
  template: `@if (mode() === 'spinner') { <div class="spinner" role="status"><mat-spinner [diameter]="diameter()"/><span>{{ label() }}</span></div> } @else { <div class="skeleton" role="status" [attr.aria-label]="label()"><span></span><span></span><span></span></div> }`,
  styles: [`.spinner { display:flex; gap:1rem; align-items:center; justify-content:center; padding:2rem; color:var(--claims-text-muted); } .skeleton { display:grid; gap:.75rem; padding:1rem 0; } .skeleton span { height:1rem; border-radius:.35rem; background:linear-gradient(90deg,#e8edf2 25%,#f6f8fa 50%,#e8edf2 75%); background-size:200% 100%; animation:pulse 1.4s infinite; } .skeleton span:nth-child(2){width:80%}.skeleton span:nth-child(3){width:60%}@keyframes pulse{to{background-position:-200% 0}}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoadingIndicatorComponent {
  readonly mode = input<'spinner' | 'skeleton'>('spinner');
  readonly label = input('Loading…');
  readonly diameter = input(36);
}
