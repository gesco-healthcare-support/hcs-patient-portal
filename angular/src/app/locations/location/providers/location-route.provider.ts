import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { LOCATION_BASE_ROUTES } from './location-base.routes';

export const LOCATIONS_LOCATION_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...LOCATION_BASE_ROUTES];
  routesService.add(routes);
}
