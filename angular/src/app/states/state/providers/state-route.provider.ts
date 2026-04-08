import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { STATE_BASE_ROUTES } from './state-base.routes';

export const STATES_STATE_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...STATE_BASE_ROUTES];
  routesService.add(routes);
}
