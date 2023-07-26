# Usage

## Settings

Edit or create new `/etc/triode/appsettings.Production.json` if not present.

### Domain rules

```json
{
  "Rules": {
    "*.dev.domain": "Proxy",
    "*.bad.domain": "Refuse"
  }
}
```

| Rule                  | Behavior                                    |
|-----------------------|---------------------------------------------|
| Pass (default)        | responds original addresses to DNS query    |
| Proxy                 | proxies as is with self-signed certificate  |
| Refuse                | responds REFUSED to DNS query               |
| Secure (experimental) | clinet -*http*-> Triode -*https*-> upstream |

### Promiscuous mode

```json
{
  "Promiscuous": true or false,
}
```

| Value           | Behavior                                 |
|-----------------|------------------------------------------|
| true            | displays requests from any addresses     |
| false (default) | displays requests only from same address |

## Advanced Usage

When a target device does not accept self-signed certificates,
some options are available:

* Install commercial CA or Let's Encrypt -issued certificates using [triode-certs](../tools/#triode-certs)
* Use "Secure" rule (experimental) to upgrade http requests to https servers

### Logging

[triode-trace](../tools/#triode-trace) outputs logs to stdout in NCSA extended/combined log format.
