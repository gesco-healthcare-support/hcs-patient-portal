import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { APPLICANT_ATTORNEY_BASE_ROUTES } from './applicant-attorney-base.routes';

export const APPLICANT_ATTORNEYS_APPLICANT_ATTORNEY_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...APPLICANT_ATTORNEY_BASE_ROUTES];
  routesService.add(routes);
}
