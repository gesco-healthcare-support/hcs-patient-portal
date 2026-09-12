import {
  ALLOWED_DOCUMENT_EXTENSIONS,
  MAX_DOCUMENT_UPLOAD_BYTES,
} from './document-upload.constants';

/**
 * AF7 (2026-06-05): client-side pre-stage validation for a booking-form
 * document, mirroring the server guards (empty, 10 MB cap, allowed extensions)
 * so an invalid file is rejected at pick time -- before staging or any upload
 * round-trip. Returns a human-readable error message, or null when the file is
 * acceptable. Pure function (no DI) so it is unit-testable in isolation, like
 * shared/attorney-section-validators.ts.
 */
export function validateDocumentFile(file: File): string | null {
  if (file.size <= 0) {
    return 'File is empty.';
  }
  if (file.size > MAX_DOCUMENT_UPLOAD_BYTES) {
    return `File exceeds the ${MAX_DOCUMENT_UPLOAD_BYTES / (1024 * 1024)} MB upload cap.`;
  }
  if (!ALLOWED_DOCUMENT_EXTENSIONS.includes(fileExtension(file.name))) {
    return `Unsupported file type. Allowed: ${ALLOWED_DOCUMENT_EXTENSIONS.join(', ')}.`;
  }
  return null;
}

/** Lower-cased extension including the leading dot, or '' when there is none. */
function fileExtension(fileName: string): string {
  const lastDot = fileName.lastIndexOf('.');
  return lastDot >= 0 ? fileName.slice(lastDot).toLowerCase() : '';
}
