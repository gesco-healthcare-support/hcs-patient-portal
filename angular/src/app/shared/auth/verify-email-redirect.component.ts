import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

/**
 * 2026-05-06 -- OLD-parity URL alias.
 *
 * OLD app shipped verification email links of the form
 * `{clientUrl}/verify-email/{userId}?query={UUID}` (see
 * P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs
 * lines 130-131). NEW emails use ABP's
 * `{spaBaseUrl}/account/email-confirmation?userId={guid}&confirmationToken={token}`.
 *
 * This component is mounted at `/verify-email/:userId` so any user clicking
 * an OLD-style link still lands on the right page. The OLD `query` param
 * is forwarded as `confirmationToken` so ABP's email-confirmation page
 * tries to validate it. The token formats are NOT compatible (OLD uses a
 * UUID stored on the User row; NEW uses an ABP DataProtection token), so
 * legacy links from the OLD system will FAIL validation -- but the user
 * lands on a page that explains this clearly instead of a 404. Users with
 * a freshly-generated NEW link work end-to-end.
 */
@Component({
  selector: 'app-verify-email-redirect',
  standalone: true,
  template: '',
})
export class VerifyEmailRedirectComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  ngOnInit(): void {
    const userId = this.route.snapshot.paramMap.get('userId') ?? '';
    const oldQuery = this.route.snapshot.queryParamMap.get('query') ?? '';
    this.router.navigate(['/account/email-confirmation'], {
      queryParams: {
        userId,
        confirmationToken: oldQuery,
      },
      replaceUrl: true,
    });
  }
}
