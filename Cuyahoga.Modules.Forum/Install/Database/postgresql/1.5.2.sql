ALTER TABLE cm_forumuser
	ADD COLUMN occupation varchar(50);

UPDATE cuyahoga_version SET major = 1, minor = 5, patch = 2 WHERE assembly = 'Cuyahoga.Modules.Forum';