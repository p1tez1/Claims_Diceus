import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatStep, MatStepper } from '@angular/material/stepper';
import { Router } from '@angular/router';
import { finalize, merge, take } from 'rxjs';
import { ApiError, CreateClaimRequest, ClaimValidation, Policy } from '../../core/models/api.models';
import { ClaimsApiService } from '../../core/services/claims-api.service';
import { buildClaimRequest, createLossForm, createPartiesForm, createReviewForm, stepForField } from './fnol-forms';
import { IntakeWarningsDialogComponent } from './intake-warnings-dialog.component';
import { LossStepComponent } from './loss-step.component';
import { PartiesStepComponent } from './parties-step.component';
import { ReviewStepComponent } from './review-step.component';

@Component({ selector: 'app-fnol-intake', standalone: true,
  imports: [MatButton, MatStep, MatStepper, LossStepComponent, PartiesStepComponent, ReviewStepComponent],
  templateUrl: './fnol-intake.component.html', styleUrl: './fnol-intake.component.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class FnolIntakeComponent {
  @ViewChild(MatStepper) stepper!: MatStepper;
  private readonly claimsApi = inject(ClaimsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly loss = createLossForm();
  readonly people = createPartiesForm();
  readonly review = createReviewForm();
  readonly pending = signal(false);
  readonly selectedPolicy = signal<Policy | null>(null);
  readonly stepErrors = signal<string[][]>([[], [], []]);
  readonly serverErrors = signal<Record<string, string[]>>({});
  private attempt: { body: string; key: string } | null = null;

  constructor() {
    this.loss.controls.unknownPolicy.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(unknown => {
      if (unknown) this.review.reset({ enabled: false, component: 'Indemnity', amount: null, changeReason: '' });
    });
    merge(this.loss.valueChanges, this.people.valueChanges, this.review.valueChanges)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => { this.stepErrors.set([[], [], []]); this.serverErrors.set({}); });
  }

  next(index: 0 | 1): void {
    const group = index === 0 ? this.loss : this.people;
    group.markAllAsTouched();
    if (group.valid) this.stepper.next();
  }

  create(): void {
    this.loss.markAllAsTouched(); this.people.markAllAsTouched(); this.review.markAllAsTouched();
    if (this.pending()) return;
    if (this.loss.invalid) { this.stepper.selectedIndex = 0; return; }
    if (this.people.invalid) { this.stepper.selectedIndex = 1; return; }
    if (this.review.invalid) return;
    const payload = buildClaimRequest(this.loss, this.people, this.review);
    this.pending.set(true);
    this.claimsApi.validate(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => this.handleValidation(result, payload),
      error: (error: HttpErrorResponse) => { this.pending.set(false); if (error.status === 422) this.mapErrors(error.error); }
    });
  }

  private handleValidation(result: ClaimValidation, payload: CreateClaimRequest): void {
    if (result.criticalIssues.length) {
      this.pending.set(false);
      const errors: Record<string, string[]> = {};
      for (const issue of result.criticalIssues) (errors[issue.field] ??= []).push(issue.message);
      this.mapErrors({ errors });
      return;
    }
    if (!result.warnings.length) { this.submit(payload); return; }
    this.dialog.open(IntakeWarningsDialogComponent, { data: result.warnings, width: '480px' }).afterClosed().pipe(take(1), takeUntilDestroyed(this.destroyRef)).subscribe(confirmed => {
      if (confirmed) this.submit(payload); else this.pending.set(false);
    });
  }

  private submit(payload: CreateClaimRequest): void {
    const body = JSON.stringify(payload);
    if (!this.attempt || this.attempt.body !== body) this.attempt = { body, key: crypto.randomUUID() };
    this.claimsApi.create(payload, this.attempt.key).pipe(finalize(() => this.pending.set(false)), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: claim => { void this.router.navigate(['/claims', claim.id], { queryParams: { created: true }, state: { created: true, claimNumber: claim.claimNumber } }); },
      error: (error: HttpErrorResponse) => { if (error.status === 422) this.mapErrors(error.error); }
    });
  }

  private mapErrors(body: Partial<ApiError>): void {
    const errors = body.errors ?? {};
    const byStep: string[][] = [[], [], []];
    for (const [field, messages] of Object.entries(errors)) {
      const step = stepForField(field);
      byStep[step].push(...messages);
      const simple = field.replace(/^InitialReserve\./, '');
      const control = step === 0 ? this.loss.get(field.charAt(0).toLowerCase() + field.slice(1))
        : step === 1 ? this.people.get(field.charAt(0).toLowerCase() + field.slice(1))
        : this.review.get(simple.charAt(0).toLowerCase() + simple.slice(1));
      if (control) { control.setErrors({ ...control.errors, server: messages[0] }); control.markAsTouched(); }
    }
    this.serverErrors.set(errors);
    this.stepErrors.set(byStep);
    const first = byStep.findIndex(messages => messages.length > 0);
    if (first >= 0) this.stepper.selectedIndex = first;
  }
}
