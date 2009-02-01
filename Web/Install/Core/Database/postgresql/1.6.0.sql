/*
 * DDL Changes
 */
ALTER TABLE cuyahoga_site
ADD offlinetemplateid int NULL;

ALTER TABLE cuyahoga_site
	ADD CONSTRAINT FK_site_template_offlinetemplateid,
	FOREIGN KEY (offlinetemplateid) REFERENCES cuyahoga_template (templateid);

ALTER TABLE cuyahoga_node
	ADD status int NOT NULL DEFAULT (0);

/*
 *  Version
 */
UPDATE cuyahoga_version SET major = 1, minor = 6, patch = 0 WHERE assembly = 'Cuyahoga.Core';
