import { MAX_DOCUMENT_UPLOAD_BYTES } from './document-upload.constants';
import { validateDocumentFile } from './document-upload.validation';

/**
 * AF7 (2026-06-05): pure-function unit tests for the client-side pre-stage
 * document guard. No TestBed -- same direct-call pattern as
 * shared/attorney-section-validators.spec.ts. `size` is overridden so tests
 * stay cheap (no large in-memory allocations).
 */
describe('validateDocumentFile', () => {
  function makeFile(name: string, size: number): File {
    const file = new File(['x'], name);
    Object.defineProperty(file, 'size', { value: size });
    return file;
  }

  it('accepts a PDF within the cap', () => {
    expect(validateDocumentFile(makeFile('records.pdf', 1024))).toBeNull();
  });

  it('accepts jpg / jpeg / png, case-insensitive', () => {
    expect(validateDocumentFile(makeFile('scan.JPG', 2048))).toBeNull();
    expect(validateDocumentFile(makeFile('scan.jpeg', 2048))).toBeNull();
    expect(validateDocumentFile(makeFile('scan.png', 2048))).toBeNull();
  });

  it('accepts a file exactly at the 10 MB cap', () => {
    expect(validateDocumentFile(makeFile('max.pdf', MAX_DOCUMENT_UPLOAD_BYTES))).toBeNull();
  });

  it('rejects an empty file', () => {
    expect(validateDocumentFile(makeFile('empty.pdf', 0))).toBe('File is empty.');
  });

  it('rejects a file one byte over the cap', () => {
    const error = validateDocumentFile(makeFile('big.pdf', MAX_DOCUMENT_UPLOAD_BYTES + 1));
    expect(error).toContain('10 MB upload cap');
  });

  it('rejects an unsupported extension', () => {
    expect(validateDocumentFile(makeFile('malware.exe', 1024))).toContain('Unsupported file type');
  });

  it('rejects a file with no extension', () => {
    expect(validateDocumentFile(makeFile('noext', 1024))).toContain('Unsupported file type');
  });
});
