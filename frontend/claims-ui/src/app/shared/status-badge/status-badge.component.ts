import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ClaimStatus } from '../../core/models/api.models';

export interface StatusPresentation { label: string; cssClass: string; }
export const STATUS_PRESENTATIONS: Record<ClaimStatus, StatusPresentation> = {
  Draft: { label: 'Draft', cssClass: 'status-draft' },
  Open: { label: 'Open', cssClass: 'status-open' },
  UnderInvestigation: { label: 'Under investigation', cssClass: 'status-under-investigation' },
  PendingPayment: { label: 'Pending payment', cssClass: 'status-pending-payment' },
  Closed: { label: 'Closed', cssClass: 'status-closed' },
  Reopened: { label: 'Reopened', cssClass: 'status-reopened' },
  Withdrawn: { label: 'Withdrawn', cssClass: 'status-withdrawn' }
};

export function statusPresentation(status: ClaimStatus): StatusPresentation { return STATUS_PRESENTATIONS[status]; }

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: '<span class="status-badge" [class]="presentation().cssClass">{{ presentation().label }}</span>',
  styleUrl: './status-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StatusBadgeComponent {
  readonly status = input.required<ClaimStatus>();
  readonly presentation = computed(() => statusPresentation(this.status()));
}
