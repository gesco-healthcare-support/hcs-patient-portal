import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { DEFENSE_ATTORNEY_BASE_ROUTES } from './defense-attorney-base.routes';

export const DEFENSE_ATTORNEYS_DEFENSE_ATTORNEY_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...DEFENSE_ATTORNEY_BASE_ROUTES];
  routesService.add(routes);
}
