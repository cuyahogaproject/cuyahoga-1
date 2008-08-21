INSERT INTO cuyahoga_modulesetting (moduletypeid, name, friendlyname, settingdatatype, iscustomtype, isrequired) 
SELECT mt.moduletypeid, 'MOVIEPARAMS', 'Set any flash params (var:value;)', 'System.String', false, false
FROM cuyahoga_moduletype mt
WHERE mt.assemblyname = 'Cuyahoga.Modules.Flash';

UPDATE cuyahoga_version SET major = 1, minor = 5, patch = 1 WHERE assembly = 'Cuyahoga.Modules.Flash';