import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle } from '@angular/material/dialog';
import { IntakeIssue } from '../../core/models/api.models';

@Component({ selector: 'app-intake-warnings-dialog', standalone: true,
  imports: [MatButton, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle],
  template: `<h2 mat-dialog-title>Review validation warnings</h2><mat-dialog-content><p>The claim can be created with these warnings:</p><ul>@for (warning of warnings; track $index) { <li>{{ warning.message }}</li> }</ul></mat-dialog-content><mat-dialog-actions align="end"><button mat-button [mat-dialog-close]="false">Back to form</button><button mat-flat-button color="primary" [mat-dialog-close]="true">Create claim</button></mat-dialog-actions>`,
  changeDetection: ChangeDetectionStrategy.OnPush })
export class IntakeWarningsDialogComponent { readonly warnings = inject<IntakeIssue[]>(MAT_DIALOG_DATA); }
