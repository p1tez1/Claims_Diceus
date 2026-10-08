import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatDialogActions, MatDialogClose, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { inject } from '@angular/core';

@Component({ selector: 'app-reserve-reject-dialog', standalone: true,
  imports: [ReactiveFormsModule, MatButton, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle, MatFormField, MatLabel, MatInput],
  template: `<h2 mat-dialog-title>Reject reserve</h2><mat-dialog-content><mat-form-field appearance="outline" style="width:100%;margin-top:.5rem"><mat-label>Reason</mat-label><textarea matInput rows="4" maxlength="500" [formControl]="reason"></textarea></mat-form-field></mat-dialog-content><mat-dialog-actions align="end"><button mat-button mat-dialog-close>Cancel</button><button mat-flat-button color="warn" [disabled]="reason.invalid" (click)="confirm()">Reject</button></mat-dialog-actions>`,
  changeDetection: ChangeDetectionStrategy.OnPush })
export class ReserveRejectDialogComponent {
  private readonly dialog = inject(MatDialogRef<ReserveRejectDialogComponent, string>);
  readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] });
  confirm(): void { if (this.reason.valid) this.dialog.close(this.reason.value.trim()); }
}
