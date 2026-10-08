import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { AuditLog, Page } from '../../core/models/api.models';
import { AuditApiService } from '../../core/services/audit-api.service';

@Component({ selector: 'app-claim-audit', standalone: true,
  imports: [DatePipe, MatButton, MatProgressSpinner],
  template: `<div class="heading"><div><h2>Audit log</h2><p>Immutable history of claim activity</p></div><button mat-button type="button" [disabled]="loading()" (click)="load(page())">Refresh</button></div>
    @if (loading()) { <div class="loading"><mat-spinner diameter="30" /> Loading audit events</div> }
    @else { <div class="events">@for (event of items(); track event.id) { <article><time>{{ event.createdAt | date:'medium' }}</time><span class="badge">{{ event.eventType }}</span><div class="description">{{ event.description }} @if (relatedLink(event); as link) { <a [href]="link" (click)="openRelated($event, event)">View related {{ event.relatedEntityType }}</a> }</div><span>{{ event.actorName || event.createdByUserId || 'System' }}</span></article> } @empty { <p>No audit events recorded.</p> }</div>
      <nav aria-label="Audit pages"><button mat-button [disabled]="page() <= 1 || loading()" (click)="load(page()-1)">Previous</button><span>Page {{ page() }} of {{ totalPages() }}</span><button mat-button [disabled]="page() >= totalPages() || loading()" (click)="load(page()+1)">Next</button></nav> }`,
  styles: [`:host{display:block}.heading,.events article,nav{display:flex;align-items:center;gap:1rem}.heading{justify-content:space-between}.heading h2{margin:0}.heading p{margin:.2rem 0 1rem;color:var(--claims-text-muted)}.events article{padding:.85rem 0;border-bottom:1px solid var(--claims-border)}time{min-width:145px;color:var(--claims-text-muted);font-size:.85rem}.badge{min-width:150px;border-radius:999px;padding:.25rem .5rem;background:#e6f2f4;color:var(--claims-primary-dark);font-size:.72rem;font-weight:700;text-align:center}.description{flex:1}.description a{display:block;margin-top:.25rem;color:var(--claims-primary)}nav{justify-content:flex-end;margin-top:1rem}.loading{display:flex;align-items:center;gap:1rem;min-height:160px}@media(max-width:700px){.events article{align-items:flex-start;flex-direction:column}}`],
  changeDetection: ChangeDetectionStrategy.OnPush })
export class ClaimAuditComponent {
  @Input({ required: true }) claimId!: string;
  @Output() readonly relatedSelected = new EventEmitter<{ tab: number; id: string }>();
  private readonly api = inject(AuditApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly page = signal(1);
  readonly pageSize = 20;
  readonly data = signal<Page<AuditLog>>({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  readonly loading = signal(true);
  readonly items = () => this.data().items;
  readonly totalPages = () => Math.max(1, Math.ceil(this.data().totalCount / this.pageSize));
  ngOnInit(): void { this.load(1); }
  load(page: number): void { if (page < 1 || (page > this.totalPages() && this.data().totalCount > 0)) return; this.loading.set(true); this.api.get(this.claimId, page, this.pageSize).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: result => { this.data.set({ ...result, items: [...result.items].sort((a, b) => b.createdAt.localeCompare(a.createdAt)) }); this.page.set(result.page); this.loading.set(false); }, error: () => this.loading.set(false) }); }
  relatedLink(event: AuditLog): string | null {
    if (!event.relatedEntityId) return null;
    if (event.relatedEntityType === 'ReserveHistory') return '#reserve-' + event.relatedEntityId;
    if (event.relatedEntityType === 'ClaimDocument') return '#document-' + event.relatedEntityId;
    if (event.relatedEntityType === 'ClaimParty') return '#party-' + event.relatedEntityId;
    return null;
  }
  openRelated(click: Event, event: AuditLog): void {
    const link = this.relatedLink(event); if (!link) return;
    click.preventDefault();
    this.relatedSelected.emit({ tab: event.relatedEntityType === 'ClaimParty' ? 1 : event.relatedEntityType === 'ReserveHistory' ? 2 : 3, id: link.slice(1) });
  }
}
