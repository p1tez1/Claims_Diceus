import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle } from '@angular/material/dialog';
import { MatButton } from '@angular/material/button';

export interface ConfirmDialogData { title: string; message: string; confirmLabel?: string; cancelLabel?: string; destructive?: boolean; }

@Component({
  selector: 'app-confirm-dialog', standalone: true,
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatDialogClose, MatButton],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>{{ data.message }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="false">{{ data.cancelLabel ?? 'Cancel' }}</button>
      <button mat-flat-button [color]="data.destructive ? 'warn' : 'primary'" [mat-dialog-close]="true">{{ data.confirmLabel ?? 'Confirm' }}</button>
    </mat-dialog-actions>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConfirmDialogComponent { readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA); }
