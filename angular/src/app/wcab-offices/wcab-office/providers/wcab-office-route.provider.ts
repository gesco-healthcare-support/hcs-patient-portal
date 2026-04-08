import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { WCAB_OFFICE_BASE_ROUTES } from './wcab-office-base.routes';

export const WCAB_OFFICES_WCAB_OFFICE_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...WCAB_OFFICE_BASE_ROUTES];
  routesService.add(routes);
}
