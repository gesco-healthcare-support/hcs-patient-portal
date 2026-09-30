import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { RestService } from '@abp/ng.core';
import { of, throwError } from 'rxjs';
import { PublicDocumentUploadComponent } from './public-document-upload.component';
import { ALLOWED_DOCUMENT_EXTENSIONS } from '../appointment-documents/document-upload.constants';

/**
 * The upload itself: selection, the client-side gates, and the error messages.
 *
 * A SIBLING to public-document-upload-link.spec.ts rather than an extension of it. That describe
 * is named "link validity (#628)" and covers the route parameters; load and error behaviour does
 * not belong under a name that says otherwise, because a failing test reporting under the wrong
 * name has lost most of its diagnostic value.
 *
 * THE MOST IMPORTANT TEST IN THIS FILE IS THE 403/404 ONE. This is an ANONYMOUS page. The
 * component drives its message from the HTTP status and deliberately gives 403 and 404 the same
 * text, so a caller cannot tell "this code is wrong" from "this appointment is closed" -- that
 * difference would be an enumeration oracle for probing which verification codes exist. Nothing
 * pinned it before this spec, and distinguishing the two reads like a copy-paste tidy-up.
 */
describe('PublicDocumentUploadComponent upload', () => {
  let request: jasmine.Spy;

  // A real extension taken from the shared allow-list rather than hardcoded, for the reason the
  // component itself records: a local copy of this list had already drifted once.
  const goodExtension = ALLOWED_DOCUMENT_EXTENSIONS[0].replace(/^\./, '');

  function configure(id = 'appt-1', verificationCode = 'code-1'): PublicDocumentUploadComponent {
    TestBed.resetTestingModule();
    request = jasmine.createSpy('request').and.returnValue(of(undefined));
    TestBed.configureTestingModule({
      imports: [PublicDocumentUploadComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) =>
                  key === 'id' ? id : key === 'verificationCode' ? verificationCode : null,
              },
            },
          },
        },
        { provide: RestService, useValue: { request } },
      ],
    });
    return TestBed.createComponent(PublicDocumentUploadComponent).componentInstance;
  }

  function fileOf(name: string, bytes = 1024): File {
    const file = new File(['x'], name);
    // Overridden rather than allocating megabytes of real content.
    Object.defineProperty(file, 'size', { value: bytes });
    return file;
  }

  function failWith(status: number): void {
    request.and.returnValue(throwError(() => new HttpErrorResponse({ status })));
  }

  describe('selecting a file', () => {
    it('takes the picked file and clears any previous error', () => {
      const cmp = configure();
      cmp.errorMessage = 'previous';
      const file = fileOf(`doc.${goodExtension}`);
      cmp.onFileSelected({ target: { files: [file] } } as unknown as Event);
      expect(cmp.selectedFile).toBe(file);
      expect(cmp.errorMessage).toBe('');
    });

    it('clears the selection when the picker returns nothing', () => {
      const cmp = configure();
      cmp.onFileSelected({ target: { files: [] } } as unknown as Event);
      expect(cmp.selectedFile).toBeNull();
    });

    it('accepts a dropped file exactly as a picked one', () => {
      const cmp = configure();
      const file = fileOf(`doc.${goodExtension}`);
      cmp.onFilesDropped([file]);
      expect(cmp.selectedFile).toBe(file);
    });

    it('takes the first of several dropped files, since one link is one document', () => {
      const cmp = configure();
      const first = fileOf(`first.${goodExtension}`);
      cmp.onFilesDropped([first, fileOf(`second.${goodExtension}`)]);
      expect(cmp.selectedFile).toBe(first);
    });

    it('ignores an empty drop', () => {
      const cmp = configure();
      cmp.onFilesDropped([]);
      expect(cmp.selectedFile).toBeNull();
    });

    it('ignores a drop while an upload is in flight', () => {
      const cmp = configure();
      cmp.state = 'uploading';
      cmp.onFilesDropped([fileOf(`doc.${goodExtension}`)]);
      expect(cmp.selectedFile).toBeNull();
    });
  });

  describe('the client-side gates', () => {
    it('does nothing without a selected file', () => {
      configure().upload();
      expect(request).not.toHaveBeenCalled();
    });

    it('does nothing while an upload is already in flight', () => {
      const cmp = configure();
      cmp.selectedFile = fileOf(`doc.${goodExtension}`);
      cmp.state = 'uploading';
      cmp.upload();
      expect(request).not.toHaveBeenCalled();
    });

    it('does nothing when the link is missing its verification code', () => {
      const cmp = configure('appt-1', '');
      cmp.selectedFile = fileOf(`doc.${goodExtension}`);
      cmp.upload();
      expect(request).not.toHaveBeenCalled();
    });

    it('rejects an extension outside the shared allow-list without calling out', () => {
      const cmp = configure();
      cmp.selectedFile = fileOf('payload.exe');
      cmp.upload();
      expect(request).not.toHaveBeenCalled();
      expect(cmp.state).toBe('error');
      expect(cmp.errorMessage).toBe('Please choose a PDF, Word (.docx), JPG, or PNG file.');
    });

    it('rejects a file over the 10 MB cap without calling out', () => {
      const cmp = configure();
      cmp.selectedFile = fileOf(`doc.${goodExtension}`, cmp.maxBytes + 1);
      cmp.upload();
      expect(request).not.toHaveBeenCalled();
      expect(cmp.errorMessage).toBe('File exceeds the 10 MB limit.');
    });

    it('accepts a file exactly at the cap', () => {
      // The boundary on the inclusive side: the guard is `>`, not `>=`.
      const cmp = configure();
      cmp.selectedFile = fileOf(`doc.${goodExtension}`, cmp.maxBytes);
      cmp.upload();
      expect(request).toHaveBeenCalled();
    });

    // The positive control for all five refusals above.
    it('uploads a valid file', () => {
      const cmp = configure();
      cmp.selectedFile = fileOf(`doc.${goodExtension}`);
      cmp.upload();
      expect(request).toHaveBeenCalled();
      expect(cmp.state).toBe('success');
    });
  });

  describe('the request', () => {
    it('posts to the by-code path carrying both route parameters', () => {
      const cmp = configure('appt-9', 'code-9');
      cmp.selectedFile = fileOf(`doc.${goodExtension}`);
      cmp.upload();
      const [config] = request.calls.mostRecent().args as [
        { method: string; url: string; body: unknown },
      ];
      expect(config.method).toBe('POST');
      expect(config.url).toBe('/api/public/appointment-documents/appt-9/upload-by-code/code-9');
      expect(config.body instanceof FormData).toBeTrue();
    });

    it('owns its error UX rather than letting the app shell handle it', () => {
      // skipHandleError matters on this page specifically: there is no app shell
      // here to host ABP's global error modal.
      const cmp = configure();
      cmp.selectedFile = fileOf(`doc.${goodExtension}`);
      cmp.upload();
      const [, options] = request.calls.mostRecent().args as [
        unknown,
        { skipHandleError: boolean },
      ];
      expect(options.skipHandleError).toBeTrue();
    });
  });

  describe('error messages are driven by status, never by the server body', () => {
    function messageFor(status: number): string {
      const cmp = configure();
      failWith(status);
      cmp.selectedFile = fileOf(`doc.${goodExtension}`);
      cmp.upload();
      expect(cmp.state).toBe('error');
      return cmp.errorMessage;
    }

    /**
     * THE ANTI-ENUMERATION PROPERTY. An anonymous caller must not be able to tell a bad
     * verification code from a valid one whose appointment is closed. If these two messages ever
     * differ, this page becomes an oracle for probing which codes exist.
     */
    it('gives 403 and 404 the SAME message, so neither confirms a code exists', () => {
      expect(messageFor(403)).toBe(messageFor(404));
    });

    it('and that shared message names neither cause', () => {
      expect(messageFor(404)).toBe(
        'This upload link is invalid or has expired, or the appointment is no longer accepting documents.',
      );
    });

    it('asks the caller to wait after too many attempts', () => {
      expect(messageFor(429)).toBe('Too many attempts. Please wait a while and try again.');
    });

    it('reports a server-side size rejection distinctly from a bad link', () => {
      expect(messageFor(413)).toBe('That file is too large. Please upload a file up to 10 MB.');
    });

    it('reports a connection failure as a network problem', () => {
      expect(messageFor(0)).toBe('Network error. Check your connection and try again.');
    });

    it('falls back to a generic message for anything else', () => {
      expect(messageFor(500)).toBe('Sorry, we could not upload your document. Please try again.');
    });
  });
});
