# Docker-Compose for Infrastructure Dependencies

This folder comes from the ABP solution template. Its scripts start **Redis only**, for running the .NET hosts
directly on your machine. SQL Server and MinIO are not included here.

For the full stack (SQL Server, Redis, MinIO, the migrator, both .NET hosts, the packet renderer and the SPA), use
`docker-compose.yml` at the repository root, described in
[docs/runbooks/DOCKER-DEV.md](../../docs/runbooks/DOCKER-DEV.md). For running the hosts without Docker, see
[docs/runbooks/LOCAL-DEV.md](../../docs/runbooks/LOCAL-DEV.md).

> These dependencies are also configured in the ABP Studio Solution Runner. If you use it, there is no need to run
> these scripts.

## Up

Run `up.ps1` in a PowerShell terminal from this folder. It creates the `caseevaluation` Docker network and starts
the `redis` container from `containers/redis.yml` on port 6379.

Note that `containers/redis.yml` pins its own Redis tag, which is not the one the root `docker-compose.yml` uses.

## Down

Run `down.ps1` to stop and remove the Redis container. It leaves the `caseevaluation` network in place.
