import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { APPOINTMENT_BASE_ROUTES } from './appointment-base.routes';

export const APPOINTMENTS_APPOINTMENT_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...APPOINTMENT_BASE_ROUTES];
  routesService.add(routes);
}
