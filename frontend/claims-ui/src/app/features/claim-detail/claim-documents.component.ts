import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse, HttpEventType } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, Input, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatOption } from '@angular/material/core';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { finalize } from 'rxjs';
import { ApiError, DocumentInfo } from '../../core/models/api.models';
import { DocumentsApiService } from '../../core/services/documents-api.service';

const MAX_BYTES = 50 * 1024 * 1024;
const EXTENSIONS = ['pdf', 'jpg', 'jpeg', 'png', 'docx', 'xlsx', 'txt', 'csv'];

@Component({ selector: 'app-claim-documents', standalone: true,
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule, MatButton, MatOption, MatFormField, MatLabel, MatProgressBar, MatProgressSpinner, MatSelect],
  template: `<div class="heading"><div><h2>Documents</h2><p>Files attached to this claim</p></div><button mat-button [disabled]="loading()" (click)="refresh()">Refresh</button></div>
    <div class="upload"><mat-form-field appearance="outline"><mat-label>Document type</mat-label><mat-select [formControl]="documentType"><mat-option value="Evidence">Evidence</mat-option><mat-option value="Correspondence">Correspondence</mat-option><mat-option value="Report">Report</mat-option><mat-option value="Other">Other</mat-option></mat-select></mat-form-field>
      <input #picker type="file" [accept]="accept" [disabled]="uploading()" (change)="selectFile($event)" aria-label="Choose document">
      <button mat-flat-button color="primary" [disabled]="!selectedFile() || documentType.invalid || uploading()" (click)="upload()">@if (uploading()) { <mat-spinner diameter="18" /> } Upload</button></div>
    @if (uploading()) { <mat-progress-bar mode="determinate" [value]="progress()" aria-label="Upload progress" /> <p>{{ progress() }}% uploaded</p> }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (loading()) { <div class="loading"><mat-spinner diameter="30" /> Loading documents</div> }
    @else { <div class="table-wrap"><table><thead><tr><th>Name</th><th>Type</th><th>Uploaded</th><th>Uploader</th><th>Size</th><th></th></tr></thead><tbody>@for (doc of documents(); track doc.id) { <tr [id]="'document-'+doc.id"><td>{{ doc.documentName }}</td><td>{{ doc.documentType }}</td><td>{{ doc.uploadedAt | date:'medium' }}</td><td>{{ doc.uploaderName || doc.uploadedByUserId || '—' }}</td><td>{{ doc.fileSizeBytes / 1024 | number:'1.0-0' }} KB</td><td><button mat-button type="button" [disabled]="downloading() !== null" (click)="download(doc)">Download</button></td></tr> } @empty { <tr><td colspan="6" class="empty">No documents uploaded.</td></tr> }</tbody></table></div> }`,
  styles: [`:host{display:block}.heading,.upload{display:flex;align-items:center;justify-content:space-between;gap:1rem}.heading h2{margin:0}.heading p{margin:.2rem 0 1rem;color:var(--claims-text-muted)}.upload{justify-content:flex-start;flex-wrap:wrap;margin:1rem 0}.upload mat-form-field{min-width:180px}.upload mat-spinner{display:inline-block;vertical-align:middle;margin-right:.3rem}.error{color:#991b1b;background:#fee2e2;padding:.75rem;border-radius:.5rem}.loading{display:flex;gap:1rem;align-items:center;min-height:150px}.table-wrap{overflow:auto}table{border-collapse:collapse;width:100%;min-width:650px;text-align:left}th,td{padding:.75rem;border-bottom:1px solid var(--claims-border)}th{color:var(--claims-text-muted);font-size:.8rem}.empty{text-align:center;color:var(--claims-text-muted)}`],
  changeDetection: ChangeDetectionStrategy.OnPush })
export class ClaimDocumentsComponent {
  @Input({ required: true }) claimId!: string;
  @Input() focusId: string | null = null;
  @ViewChild('picker') picker?: ElementRef<HTMLInputElement>;
  private readonly api = inject(DocumentsApiService);
  private readonly snack = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  readonly accept = EXTENSIONS.map(extension => '.' + extension).join(',');
  readonly documentType = new FormControl('Evidence', { nonNullable: true, validators: Validators.required });
  readonly documents = signal<DocumentInfo[]>([]);
  readonly selectedFile = signal<File | null>(null);
  readonly loading = signal(true);
  readonly uploading = signal(false);
  readonly progress = signal(0);
  readonly error = signal<string | null>(null);
  readonly downloading = signal<string | null>(null);
  ngOnInit(): void { this.refresh(); }
  refresh(): void { this.loading.set(true); this.api.list(this.claimId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: docs => { this.documents.set(docs); this.loading.set(false); if (this.focusId) setTimeout(() => document.getElementById(this.focusId!)?.scrollIntoView({ block: 'center' }), 50); }, error: () => this.loading.set(false) }); }
  selectFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.selectedFile.set(null); this.error.set(null);
    if (!file) return;
    if (file.size > MAX_BYTES) { this.error.set('The file must not exceed 50 MB.'); return; }
    if (file.size === 0) { this.error.set('The file must not be empty.'); return; }
    if (!EXTENSIONS.includes(file.name.split('.').pop()?.toLowerCase() ?? '')) { this.error.set('Allowed extensions: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV.'); return; }
    this.selectedFile.set(file);
  }
  upload(): void {
    const file = this.selectedFile(); if (!file || this.documentType.invalid || this.uploading()) return;
    this.error.set(null); this.uploading.set(true); this.progress.set(0);
    this.api.uploadWithProgress(this.claimId, file, this.documentType.value).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: event => {
      if (event.type === HttpEventType.UploadProgress) this.progress.set(Math.round(100 * event.loaded / (event.total || file.size)));
      if (event.type === HttpEventType.Response) { this.uploading.set(false); this.progress.set(100); this.selectedFile.set(null); if (this.picker) this.picker.nativeElement.value = ''; this.snack.open('Document uploaded', undefined, { duration: 3000 }); this.refresh(); }
    }, error: (error: HttpErrorResponse) => { this.uploading.set(false); this.error.set(error.status === 422 ? Object.values((error.error as Partial<ApiError>)?.errors ?? {}).flat()[0] ?? 'The file was rejected.' : 'Upload failed. Please try again.'); } });
  }
  download(document: DocumentInfo): void {
    if (this.downloading()) return;
    this.downloading.set(document.id); this.error.set(null);
    this.api.list(this.claimId).pipe(finalize(() => this.downloading.set(null)), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: docs => {
        this.documents.set(docs);
        const url = docs.find(doc => doc.id === document.id)?.downloadUrl;
        if (url) window.open(url, '_blank', 'noopener,noreferrer');
        else this.error.set('Download failed. Refresh the documents and try again.');
      },
      error: () => this.error.set('Download failed. Please try again.')
    });
  }
}
