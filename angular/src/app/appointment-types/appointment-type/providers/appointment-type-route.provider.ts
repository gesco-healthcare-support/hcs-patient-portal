import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { APPOINTMENT_TYPE_BASE_ROUTES } from './appointment-type-base.routes';

export const APPOINTMENT_TYPES_APPOINTMENT_TYPE_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...APPOINTMENT_TYPE_BASE_ROUTES];
  routesService.add(routes);
}
