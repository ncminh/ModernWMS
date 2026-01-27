CREATE TABLE sku_safety_stock (
    id SERIAL PRIMARY KEY,
    sku_id INT,
    warehouse_id INT,
    safety_stock_qty INT,
    CONSTRAINT fk_sku FOREIGN KEY (sku_id) REFERENCES sku (id),
    CONSTRAINT fk_warehouse FOREIGN KEY (warehouse_id) REFERENCES warehouse (id)
);

create table action_log (
	id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
	vue_path TEXT,
	user_name TEXT,
	action_content TEXT,
	action_time TEXT,
	tenant_id INTEGER
);

create table asnmaster (
	id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
	asn_no TEXT,
	asn_batch TEXT,
	estimated_arrival_time TEXT,
	asn_status BLOB,
	weight FLOAT,
	volume FLOAT,
	goods_owner_id INTEGER,
	goods_owner_name TEXT,
	creator TEXT,
	create_time TEXT,
	last_update_time TEXT,
	tenant_id INTEGER
);

CREATE TABLE IF NOT EXISTS global_unique_serial (
    id                INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    table_name        TEXT NOT NULL DEFAULT '',
    prefix_char       TEXT NOT NULL DEFAULT '',
    reset_rule        TEXT NOT NULL DEFAULT '',
    current_no        INTEGER NOT NULL DEFAULT 1,
    last_update_time  TEXT NOT NULL, -- store ISO-8601 (e.g., 2024-01-27T12:34:56Z)
    tenant_id         INTEGER NOT NULL DEFAULT 1
);