import { RoutesService, eLayoutType } from '@abp/ng.core';
import { inject, provideAppInitializer } from '@angular/core';

export const APP_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routes = inject(RoutesService);
  routes.add([
    {
      path: '/',
      name: '::Menu:Home',
      iconClass: 'fas fa-home',
      order: 1,
      layout: eLayoutType.application,
    },
    {
      path: '/dashboard',
      name: '::Menu:Dashboard',
      iconClass: 'fas fa-chart-line',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'CaseEvaluation.Dashboard.Host  || CaseEvaluation.Dashboard.Tenant',
    },
    // 2026-05-15 -- new top-level "User Management" menu so internal
    // staff can navigate to the invite-external-user form. The parent
    // entry is a menu container (no own route) gated by the
    // UserManagement Default permission; the child links to
    // /users/invite gated by the more specific InviteExternalUser
    // permission so future siblings (revoke, invite history) can grow
    // without re-mapping seeder grants.
    {
      path: '',
      name: '::Menu:UserManagement',
      iconClass: 'fas fa-users-cog',
      order: 100,
      layout: eLayoutType.application,
      requiredPolicy: 'CaseEvaluation.UserManagement',
    },
    {
      path: '/users/invite',
      name: '::Menu:InviteExternalUser',
      parentName: '::Menu:UserManagement',
      iconClass: 'fas fa-envelope',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'CaseEvaluation.UserManagement.InviteExternalUser',
    },
  ]);
}
