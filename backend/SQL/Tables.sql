CREATE TABLE sku_safety_stock (
    id SERIAL PRIMARY KEY,
    sku_id INT,
    warehouse_id INT,
    safety_stock_qty INT,
    CONSTRAINT fk_sku FOREIGN KEY (sku_id) REFERENCES sku (id),
    CONSTRAINT fk_warehouse FOREIGN KEY (warehouse_id) REFERENCES warehouse (id)
);

create table asnmaster (
	id SERIAL PRIMARY KEY,
	asn_no TEXT,
	asn_batch TEXT,
	estimated_arrival_time TEXT,
	asn_status BLOB,
	weight FLOAT,
	volume FLOAT,
	goods_owner_id INTEGER,
	creator TEXT,
	create_time TEXT,
	last_update_time TEXT,
	tenant_id INTEGER,

);