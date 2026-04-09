#!/bin/sh
set -e

# Generate appsettings.secrets.json from ABP_LICENSE_CODE env var at container startup
# This avoids baking secrets into Docker images
if [ -n "$ABP_LICENSE_CODE" ]; then
  echo "{\"AbpLicenseCode\": \"$ABP_LICENSE_CODE\"}" > /app/appsettings.secrets.json
else
  echo '{}' > /app/appsettings.secrets.json
fi

exec "$@"
