import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { APPOINTMENT_STATUS_BASE_ROUTES } from './appointment-status-base.routes';

export const APPOINTMENT_STATUSES_APPOINTMENT_STATUS_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...APPOINTMENT_STATUS_BASE_ROUTES];
  routesService.add(routes);
}
