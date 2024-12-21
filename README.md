# SecretKeeper

Basic resiliency daemon around a [Secret Network](https://github.com/scrtlabs/SecretNetwork) blockchain node.

## Features

- Auto restart on consensus failure `SGX_ERROR_BUSY`
- Auto rollback & restart on consensus failure `wrong Block.Header.AppHash` or `wrong Block.Header.LastResultsHash`
- Discord notify on any other consensus failure
- Discord notify if no new block was processed in given period of time

## Usage

> [!WARNING]
> Only supported on Linux

```sh
./SecretKeeper start
--secretd /usr/local/bin/secretd #Path to systemd binary
--service secret-node #Systemd service name of node
--secretd-home /home/node/.secretd #Path to your secretd home (optional, will fallback to default)
--discord-user-id  #User id on discord (optional)
--webhook-url #Discord webhook url (optional)
--webhook-username #Username of the webhook (optional)
--max-seconds-without-block #Maximum seconds without a block before a notification is sent
```

### Permission Stuff
- Make sure that SecretKeeper can acceess both `/bin/journalctl` and `/sbin/service`
- If you decide to run SecretKeeper as root and the node as a user specify the `secretd-home` parameter