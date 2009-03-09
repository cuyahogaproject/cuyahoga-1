/*
 * DDL Changes
 */
ALTER TABLE cuyahoga_site
	ADD offlinetemplateid int NULL
go

ALTER TABLE cuyahoga_site
	ADD CONSTRAINT FK_site_template_offlinetemplateid
		FOREIGN KEY (offlinetemplateid) REFERENCES cuyahoga_template (templateid)
go

ALTER TABLE cuyahoga_node
	ADD status int NULL DEFAULT (0)
go

UPDATE cuyahoga_node
	SET status = 0
go

ALTER TABLE cuyahoga_node
	ALTER COLUMN status int NOT NULL
go


/* 
 * Offline template 
 */
INSERT INTO cuyahoga_template (templateid, name, basepath, templatecontrol, css, inserttimestamp, updatetimestamp) 
VALUES (5, 'Offline', 'Templates/AnotherRed', 'Offline.ascx', 'red.css', '2004-01-26 21:52:52.365', '2004-01-26 21:52:52.365')
go
/*
 *  Version
 */
UPDATE cuyahoga_version SET major = 1, minor = 6, patch = 0 WHERE assembly = 'Cuyahoga.Core'
go