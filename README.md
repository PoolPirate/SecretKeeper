# SecretKeeper

Basic resiliency daemon around a [Secret Network](https://github.com/scrtlabs/SecretNetwork) blockchain node.

## Features

- Auto restart on consensus failure `SGX_ERROR_BUSY`
- Auto rollback & restart on consensus failure `wrong Block.Header.AppHash`
- Discord notify on any other consensus failure
- Discord notify is no new block arrived in given period of time

## Usage

> [!WARNING]
> Only supported on Linux

```sh
./SecretKeeper start
--secretd /usr/local/bin/secretd #Path to systemd binary
--service secret-node #Systemd service name of node
--discord-user-id  #User id on discord (optional)
--webhook-url #Discord webhook url (optional)
--maxSecondsWithoutBlock #Maximum seconds without a block before a notification is sent
```
