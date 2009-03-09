/*
 * DDL Changes
 */
ALTER TABLE cuyahoga_site
	ADD COLUMN offlinetemplateid int4;

ALTER TABLE cuyahoga_site
	ADD CONSTRAINT FK_site_template_offlinetemplateid
		FOREIGN KEY (offlinetemplateid) REFERENCES cuyahoga_template (templateid);

ALTER TABLE cuyahoga_node
	ADD COLUMN status int4 DEFAULT 0;

UPDATE cuyahoga_node
	SET status = 0;

ALTER TABLE cuyahoga_node
	ALTER COLUMN status SET NOT NULL;

/* 
 * Offline template 
 */
INSERT INTO cuyahoga_template (templateid, name, basepath, templatecontrol, css, inserttimestamp, updatetimestamp) 
VALUES (5, 'Offline', 'Templates/AnotherRed', 'Offline.ascx', 'red.css', '2004-01-26 21:52:52.365', '2004-01-26 21:52:52.365');

/*
 *  Version
 */
UPDATE cuyahoga_version SET major = 1, minor = 6, patch = 0 WHERE assembly = 'Cuyahoga.Core';
