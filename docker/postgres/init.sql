-- Schema for the ImageStamp composition service.
-- Mounted into /docker-entrypoint-initdb.d and executed the first time the
-- postgres container initialises an empty data directory.

CREATE TABLE IF NOT EXISTS compositions
(
    id                   uuid PRIMARY KEY,
    created_at           timestamptz NOT NULL DEFAULT now(),
    status               text        NOT NULL,
    base_image_file_name text        NULL,
    layer_count          integer     NOT NULL DEFAULT 0,
    output_size_bytes    bigint      NULL,
    processing_time_ms   integer     NULL,
    error_message        text        NULL
);

CREATE TABLE IF NOT EXISTS composition_layers
(
    id             uuid PRIMARY KEY,
    composition_id uuid    NOT NULL REFERENCES compositions (id) ON DELETE CASCADE,
    layer_type     text    NOT NULL,
    x              integer NOT NULL,
    y              integer NOT NULL,
    opacity        real    NOT NULL,
    z_index        integer NOT NULL,
    file_name      text    NULL,
    width          integer NULL,
    height         integer NULL,
    color          text    NULL
);

CREATE INDEX IF NOT EXISTS ix_compositions_created_at
    ON compositions (created_at DESC);

CREATE INDEX IF NOT EXISTS ix_composition_layers_composition_id
    ON composition_layers (composition_id);
