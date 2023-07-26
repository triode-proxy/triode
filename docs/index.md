# Triode: DNS-based selective HTTP debugging proxy

Triode is a DNS and HTTP server working as an alternative to HTTP proxy for debugging.
It answers its own IP addresses to DNS A or AAAA record queries if those domains are configured to handle,
otherwise it transfers the queries to upstream name server and keeps the original answers as is.
Triode uses TLS certificates automatically issued by its own self-signed CA for HTTPS connections.

## Motivation

Since standard HTTP proxy handles all requests, higher throughput and much RAMs are required
for debugging high traffic sites, such as video streaming, even if we want to watch text requests.
Triode can control what requests should be proxied on a per-doamin basis, using DNS technology.
It reduces bulk of traffic, and works with less RAM usage.

## Comparison

|                                              | Triode                | Other HTTP proxies |
|----------------------------------------------|-----------------------|--------------------|
| What network configuration is required?      | DNS                   | HTTP proxy         |
| What HTTP requests are handled by the proxy? | only selected domains | all requests       |
| What ports are supported?                    | 80 http and 443 https | any custom ports   |
