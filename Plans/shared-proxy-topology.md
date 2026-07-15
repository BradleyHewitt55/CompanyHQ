# Shared proxy topology

## Goal

Run one server-level reverse proxy for all companies; company applications must be private to the Docker network.

## Scope

- Replace per-company Caddy services with one server-level Caddy, Nginx, or Traefik service.
- Publish only ports 80 and 443 from the shared proxy.
- Generate company application containers without host port mappings.

## Task-specific tests

- Generate a server deployment with two companies and inspect Compose output.
- Assert exactly one proxy service publishes `80` and `443`.
- Assert neither application container nor Postgres has a published host port.
- Assert both applications share the internal Docker network with the proxy.
