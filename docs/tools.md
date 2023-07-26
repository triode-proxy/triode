# Tools

## triode-certs

imports or lists certificates.

`sudo triode-certs --import` installs from:

* PEM-formatted certificate (\*.crt) and private key (\*.key) pairs in current directory
* system-wide installed Let's Encrypt certificates

`sudo triode-certs --list` displays all certificates including self-signed and imported.

## triode-trace

outputs logs to stdout in NCSA extended/combined log format.
