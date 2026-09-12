import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { CHANGE_REQUEST_BASE_ROUTES } from './change-request-base.routes';

export const APPOINTMENTS_CHANGE_REQUEST_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...CHANGE_REQUEST_BASE_ROUTES];
  routesService.add(routes);
}
