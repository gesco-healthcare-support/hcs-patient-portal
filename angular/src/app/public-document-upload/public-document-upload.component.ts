import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { RestService } from '@abp/ng.core';

type UploadState = 'idle' | 'uploading' | 'success' | 'error';

/**
 * PR4 public document-upload page. Anonymous (no login): reached by the
 * per-document verification-code link emailed to a patient. Reads the
 * document id + code from the route, uploads the chosen file straight to the
 * existing [AllowAnonymous] endpoint, and reports the outcome itself
 * (skipHandleError) rather than via the app-shell error UI -- there is no app
 * shell on this route (eLayoutType.empty). Server gates (code match,
 * appointment open, file format/size, 5/hr rate-limit) are the source of truth;
 * the client checks size/type only for a faster, friendlier message.
 */
@Component({
  selector: 'app-public-document-upload',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [CommonModule],
  templateUrl: './public-document-upload.component.html',
  styles: [
    `
      .public-upload {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: #f1f3f5;
        padding: 24px;
      }
      .card {
        background: #fff;
        border-radius: 8px;
        box-shadow: 0 1px 4px rgba(0, 0, 0, 0.12);
        padding: 28px;
        max-width: 480px;
        width: 100%;
      }
      h1 {
        font-size: 1.4rem;
        margin-bottom: 12px;
        color: #2c3e8c;
      }
      .hint {
        color: #6c757d;
        font-size: 0.9rem;
        margin-bottom: 16px;
      }
      input[type='file'] {
        display: block;
        width: 100%;
        margin-bottom: 16px;
      }
      .msg {
        padding: 10px 12px;
        border-radius: 4px;
        font-size: 0.9rem;
        margin: 12px 0;
      }
      .msg.success {
        background: #d1e7dd;
        color: #0f5132;
      }
      .msg.error {
        background: #f8d7da;
        color: #842029;
      }
      button {
        background: #198754;
        color: #fff;
        border: 0;
        border-radius: 4px;
        padding: 10px 18px;
        cursor: pointer;
      }
      button:disabled {
        opacity: 0.6;
        cursor: default;
      }
    `,
  ],
})
export class PublicDocumentUploadComponent {
  private route = inject(ActivatedRoute);
  private restService = inject(RestService);

  private readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  private readonly verificationCode = this.route.snapshot.paramMap.get('verificationCode') ?? '';

  readonly maxBytes = 10 * 1024 * 1024; // matches the server app-layer cap
  private readonly acceptedExtensions = ['pdf', 'jpg', 'jpeg', 'png'];

  state: UploadState = 'idle';
  errorMessage = '';
  selectedFile: File | null = null;

  get hasValidLink(): boolean {
    return !!this.id && !!this.verificationCode;
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.errorMessage = '';
  }

  upload(): void {
    if (!this.hasValidLink || !this.selectedFile || this.state === 'uploading') {
      return;
    }

    const extension = this.selectedFile.name.split('.').pop()?.toLowerCase() ?? '';
    if (!this.acceptedExtensions.includes(extension)) {
      this.fail('Please choose a PDF, JPG, or PNG file.');
      return;
    }
    if (this.selectedFile.size > this.maxBytes) {
      this.fail(`File exceeds the ${this.maxBytes / (1024 * 1024)} MB limit.`);
      return;
    }

    const form = new FormData();
    form.append('file', this.selectedFile, this.selectedFile.name);

    this.state = 'uploading';
    this.errorMessage = '';
    this.restService
      .request<FormData, unknown>(
        {
          method: 'POST',
          url: `/api/public/appointment-documents/${this.id}/upload-by-code/${this.verificationCode}`,
          body: form,
        },
        // skipHandleError: own the error UX; there is no app shell here to host
        // ABP's global error modal.
        { apiName: 'Default', skipHandleError: true },
      )
      .subscribe({
        next: () => {
          this.state = 'success';
        },
        error: (err: HttpErrorResponse) => {
          this.state = 'error';
          this.errorMessage = this.messageFor(err);
        },
      });
  }

  private fail(message: string): void {
    this.state = 'error';
    this.errorMessage = message;
  }

  private messageFor(err: HttpErrorResponse): string {
    // Drive the message by status, not the server body: this is an anonymous
    // page, so we avoid leaking which check failed (no enumeration oracle for a
    // bad vs valid code) and never surface ABP's generic/dev-oriented text.
    switch (err.status) {
      case 429:
        return 'Too many attempts. Please wait a while and try again.';
      case 413:
        return 'That file is too large. Please upload a file up to 10 MB.';
      case 403:
      case 404:
        return 'This upload link is invalid or has expired, or the appointment is no longer accepting documents.';
      case 0:
        return 'Network error. Check your connection and try again.';
      default:
        return 'Sorry, we could not upload your document. Please try again.';
    }
  }
}
