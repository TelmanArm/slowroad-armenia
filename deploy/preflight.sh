#!/usr/bin/env bash

PGDATA_DIR=/mnt/pgdata/postgres

require_pgdata() {
  if ! findmnt -M /mnt/pgdata >/dev/null; then
    echo "FATAL: /mnt/pgdata is not mounted." >&2
    exit 1
  fi

  if ! sudo test -f "$PGDATA_DIR/PG_VERSION" && [ "${ALLOW_INIT:-0}" != "1" ]; then
    echo "FATAL: no Postgres cluster in $PGDATA_DIR. First install? Re-run with ALLOW_INIT=1." >&2
    exit 1
  fi
}
