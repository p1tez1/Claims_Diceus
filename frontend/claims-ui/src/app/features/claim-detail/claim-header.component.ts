import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatOption } from '@angular/material/core';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatSelect } from '@angular/material/select';
import { ClaimDetail, ClaimStatus, User } from '../../core/models/api.models';
import { StatusBadgeComponent, statusPresentation } from '../../shared/status-badge/status-badge.component';

@Component({
  selector: 'app-claim-header', standalone: true,
  imports: [DatePipe, MatButton, MatMenu, MatMenuItem, MatMenuTrigger, MatOption, MatFormField, MatLabel, MatSelect, StatusBadgeComponent],
  template: `
    <header class="claim-header surface">
      <div class="title-row">
        <div><p class="eyebrow">Claim detail</p><div class="claim-number"><h1>{{ claim.claimNumber }}</h1><button mat-icon-button type="button" aria-label="Copy claim number" title="Copy claim number" (click)="copyRequested.emit(claim.claimNumber)">⧉</button></div></div>
        <div class="actions"><app-status-badge [status]="claim.status" />
          <button mat-flat-button color="primary" [matMenuTriggerFor]="transitionMenu" [disabled]="!transitions.length || pending">Transition ▾</button>
          <mat-menu #transitionMenu="matMenu">@for (status of transitions; track status) { <button mat-menu-item [disabled]="pending" (click)="transitionRequested.emit(status)">Move to {{ label(status) }}</button> }</mat-menu>
        </div>
      </div>
      <dl class="facts">
        <div><dt>Policy Number</dt><dd>{{ claim.policyNumber || 'Not linked' }}</dd></div>
        <div><dt>Client Name</dt><dd>{{ claim.clientName || '—' }}</dd></div>
        <div><dt>Loss Date</dt><dd>{{ claim.lossEvent.lossDate | date:'mediumDate' }}</dd></div>
        <div><dt>Cause of Loss</dt><dd>{{ claim.lossEvent.causeOfLossCode }}</dd></div>
        <div class="assignee"><dt>Assigned Handler</dt><dd>
          @if (canAssign) { <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Assigned handler</mat-label><mat-select [disabled]="pending" [value]="claim.assignedHandlerId" (selectionChange)="assigneeChanged.emit($event.value)">@for (user of users; track user.id) { <mat-option [value]="user.id">{{ user.name }} · {{ user.role }}</mat-option> }</mat-select></mat-form-field> }
          @else { {{ assignedHandlerName || 'Unassigned' }} }
        </dd></div>
      </dl>
    </header>`,
  styles: [`
    .claim-header{padding:1.25rem 1.5rem;min-height:auto}.title-row,.claim-number,.actions{display:flex;align-items:center}.title-row{justify-content:space-between;gap:1rem}.claim-number h1{margin:0}.claim-number button{color:var(--claims-text-muted)}.eyebrow{margin:0 0 .25rem;color:var(--claims-primary);font-size:.75rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}.actions{gap:.85rem}.facts{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:1rem;margin:1.3rem 0 0;padding-top:1rem;border-top:1px solid var(--claims-border)}dt{color:var(--claims-text-muted);font-size:.75rem;font-weight:700;text-transform:uppercase}dd{margin:.3rem 0 0;font-weight:600}.assignee mat-form-field{width:100%;margin-top:-.25rem}@media(max-width:900px){.facts{grid-template-columns:repeat(2,1fr)}}@media(max-width:600px){.title-row{align-items:flex-start;flex-direction:column}.facts{grid-template-columns:1fr}}
  `], changeDetection: ChangeDetectionStrategy.OnPush
})
export class ClaimHeaderComponent {
  @Input({ required: true }) claim!: ClaimDetail;
  @Input() transitions: ClaimStatus[] = [];
  @Input() assignedHandlerName = '';
  @Input() users: User[] = [];
  @Input() canAssign = false;
  @Input() pending = false;
  @Output() readonly transitionRequested = new EventEmitter<ClaimStatus>();
  @Output() readonly assigneeChanged = new EventEmitter<string>();
  @Output() readonly copyRequested = new EventEmitter<string>();
  label(status: ClaimStatus): string { return statusPresentation(status).label; }
}
