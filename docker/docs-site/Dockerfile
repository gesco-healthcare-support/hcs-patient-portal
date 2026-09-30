# Builder image for the documentation site served at https://<BASE_DOMAIN>/docs/.
#
# Used only by scripts/hosting/build-docs-site.sh: it runs `mkdocs build --strict` over a read-only
# mount of the repository and never serves anything. Build context is .github/, so the only file sent
# is the locked requirements list.
#
# BASE IMAGE. Pinned by tag and digest like the shipping images; Dependabot's docker entry lists this
# directory so the digest keeps moving. See .github/dependabot.yml for why the tag stays beside it.
FROM python:3.12-slim-trixie@sha256:f77ac9e44ae96ef2c90b8053ea08c31f8be030f824196b0ae4db6d462c84e51f

# git: the git-revision-date-localized plugin dates every page from its commits, and the slim image
# has no git. NOT version-pinned, deliberately. Debian's mirrors keep only the current version of each
# package in a release, so an exact pin (git=1:2.47.x-...) breaks this build on the next Debian
# security update. The base digest fixes the release (Debian 13, trixie), so the version can only move
# within trixie's security and point updates of git 2.47. The build script prints `git --version` on
# every run, so each deploy records the version it used.
RUN apt-get update \
 && apt-get install -y --no-install-recommends git \
 && rm -rf /var/lib/apt/lists/*

# The documentation toolchain, hash-locked: every package must match a hash in the lock, and only
# wheels are accepted, so nothing resolves fresh and no install-time build script runs. The lock is
# generated from .github/docs-requirements.in; edit that file, never this list.
COPY docs-requirements.txt /tmp/docs-requirements.txt
RUN python -m pip install --no-cache-dir --require-hashes --only-binary ":all:" -r /tmp/docs-requirements.txt

# Run as an unprivileged user (Sonar docker:S6471). Added after the installs, which need root.
# scripts/hosting/build-docs-site.sh overrides it with --user <deploy uid>:<gid>, so the output
# belongs to the deploy user; this default covers any other use of the image. The build needs no
# home directory: the script sets HOME=/tmp and passes git's safe.directory through the environment.
# The group is created first so uid and gid are both a fixed 10001, not taken from a system range.
RUN groupadd --gid 10001 docs \
 && useradd --uid 10001 --gid 10001 --no-create-home --home-dir /nonexistent \
      --shell /usr/sbin/nologin docs
USER docs
