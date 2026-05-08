import { Component, OnInit } from '@angular/core';

/**
 * The SPA `/account/register` and `/account/login` paths used to host
 * standalone Angular registration / login components, but the demo flow
 * now uses ABP's stock AuthServer Razor pages (port 44368) instead. This
 * component is mounted on those SPA paths and immediately bounces the
 * browser to the AuthServer URL on the same subdomain so users following
 * an old link (or anyone who navigates manually) lands on the live page
 * rather than dead SPA components.
 *
 * Inputs (constructor): the AuthServer path to forward to, e.g.
 * `/Account/Register` or `/Account/Login`.
 */
@Component({
  selector: 'app-redirect-to-authserver',
  standalone: true,
  template: '',
})
export class RedirectToAuthServerRegisterComponent implements OnInit {
  ngOnInit(): void {
    const host = window.location.hostname;
    const protocol = window.location.protocol;
    const target = `${protocol}//${host}:44368/Account/Register`;
    window.location.replace(target);
  }
}
