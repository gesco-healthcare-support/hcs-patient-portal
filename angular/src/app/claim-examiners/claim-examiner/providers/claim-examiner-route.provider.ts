import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { CLAIM_EXAMINER_BASE_ROUTES } from './claim-examiner-base.routes';

export const CLAIM_EXAMINERS_CLAIM_EXAMINER_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...CLAIM_EXAMINER_BASE_ROUTES];
  routesService.add(routes);
}
