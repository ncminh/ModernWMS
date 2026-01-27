ALTER TABLE rolemenu ADD menu_action_authority TEXT; /* SQLite */

ALTER TABLE stock ADD COLUMN series_number TEXT;
ALTER TABLE stock ADD COLUMN expiry_date TEXT;
ALTER TABLE stock ADD COLUMN price FLOAT;
ALTER TABLE stock ADD COLUMN putaway_date TEXT;

ALTER TABLE dispatchpicklist ADD COLUMN expiry_date TEXT;
ALTER TABLE dispatchpicklist ADD COLUMN price FLOAT;
ALTER TABLE dispatchpicklist ADD COLUMN putaway_date TEXT;
ALTER TABLE dispatchpicklist ADD COLUMN picker_id INTEGER;
ALTER TABLE dispatchpicklist ADD COLUMN series_number TEXT;

ALTER TABLE sku ADD COLUMN bar_code TEXT;

ALTER TABLE dispatchlist ADD COLUMN pick_checker_id TEXT;
ALTER TABLE dispatchlist ADD COLUMN pick_checker INTEGER;

ALTER TABLE stockprocessdetail ADD COLUMN expiry_date TEXT;
ALTER TABLE stockprocessdetail ADD COLUMN series_number TEXT;
ALTER TABLE stockprocessdetail ADD COLUMN price FLOAT;
ALTER TABLE stockprocessdetail ADD COLUMN putaway_date TEXT;

ALTER TABLE stockmove ADD COLUMN expiry_date TEXT;
ALTER TABLE stockmove ADD COLUMN series_number TEXT;
ALTER TABLE stockmove ADD COLUMN price FLOAT;
ALTER TABLE stockmove ADD COLUMN putaway_date TEXT;

ALTER TABLE stockfreeze ADD COLUMN series_number TEXT;

ALTER TABLE stockadjust ADD COLUMN expiry_date TEXT;
ALTER TABLE stockadjust ADD COLUMN series_number TEXT;
ALTER TABLE stockadjust ADD COLUMN price FLOAT;
ALTER TABLE stockadjust ADD COLUMN putaway_date TEXT;

ALTER TABLE stocktaking ADD COLUMN expiry_date TEXT;
ALTER TABLE stocktaking ADD COLUMN series_number TEXT;
ALTER TABLE stocktaking ADD COLUMN price FLOAT;
ALTER TABLE stocktaking ADD COLUMN putaway_date TEXT;


alter table asn add column arrival_time TEXT;
alter table asn add column unload_time TEXT;
alter table asn add column unload_person_id INTEGER;
alter table asn add column unload_person TEXT;


alter table asn add column asnmaster_id INTEGER;
ALTER TABLE asn ADD COLUMN expiry_date TEXT;
ALTER TABLE asn ADD COLUMN price FLOAT;
ALTER TABLE asn ADD COLUMN putaway_date TEXT;

ALTER TABLE asnmaster ADD COLUMN goods_owner_name TEXT;