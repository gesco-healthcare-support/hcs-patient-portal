import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { RestService } from '@abp/ng.core';

import { PublicDocumentUploadComponent } from './public-document-upload.component';

/**
 * #628 marked this component's two injected services readonly (S2933), and it
 * had no spec at all.
 *
 * The behaviour worth pinning while here is `hasValidLink`. This is the
 * ANONYMOUS upload page -- it is reachable without a session, and the only
 * thing standing between a visitor and the upload form is both route
 * parameters being present. A link missing either half must not render as
 * usable. (Related: #613 records that this page is currently unreachable in
 * production; that is a routing question, not this guard.)
 */
describe('PublicDocumentUploadComponent link validity (#628)', () => {
  function create(params: Record<string, string | null>): PublicDocumentUploadComponent {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: (k: string) => params[k] ?? null } } },
        },
        { provide: RestService, useValue: { request: () => of(null) } },
      ],
    });
    return TestBed.runInInjectionContext(() => new PublicDocumentUploadComponent());
  }

  afterEach(() => TestBed.resetTestingModule());

  it('accepts a link carrying both parameters', () => {
    expect(create({ id: 'appt-1', verificationCode: 'code-1' }).hasValidLink).toBeTrue();
  });

  it('rejects a link with no verification code', () => {
    expect(create({ id: 'appt-1' }).hasValidLink).toBeFalse();
  });

  it('rejects a link with no appointment id', () => {
    expect(create({ verificationCode: 'code-1' }).hasValidLink).toBeFalse();
  });

  /** An empty string is not a code. `?? ''` makes absent and blank identical. */
  it('rejects a link whose parameters are present but blank', () => {
    expect(create({ id: '', verificationCode: '' }).hasValidLink).toBeFalse();
  });

  it('offers the shared allow-list as the file input accept attribute', () => {
    const c = create({ id: 'a', verificationCode: 'b' });
    expect(c.acceptAttribute).toContain('.pdf');
    expect(c.acceptAttribute.startsWith('.')).toBeTrue();
  });
});
