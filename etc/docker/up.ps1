docker network create caseevaluation --label=caseevaluation
docker-compose -f containers/redis.yml up -d
exit $LASTEXITCODE