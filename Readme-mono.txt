****************************************************************************
Special notes for Mono users.
****************************************************************************
- Cuyahoga 1.5.2 is build and tested on Mono 1.2.6 
  (Mono was installed with the installer on Ubuntu Linux).
- Before running the application you have to install 
  Castle.Services.Transaction.dll in the GAC. Navigate to the /bin directory
  and execute gacutil -i Castle.Services.Transaction.dll.
- In /Config/properties.config, set the value of the tempDir property to
  the appropriate directory (for example, /tmp).